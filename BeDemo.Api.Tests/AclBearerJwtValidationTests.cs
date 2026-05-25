using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>ValidateLifetime + signature: expired / not-yet-valid / malformed bearer (A2).</summary>
public class AclBearerJwtValidationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _publicFace;

	public AclBearerJwtValidationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_publicFace = factory.CreateFaceClient("public");
	}

	public void Dispose() => _publicFace.Dispose();

	private string CreateJwt(string role, DateTime notBefore, DateTime expires)
	{
		using var scope = _factory.Services.CreateScope();
		var keySvc = scope.ServiceProvider.GetRequiredService<IECDSAKeyService>();
		var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
		var issuer = config["Jwt:Issuer"] ?? "BeDemoApi";
		var audience = config["Jwt:Audience"] ?? "BeDemoApi";
		var creds = new SigningCredentials(keySvc.GetSigningKey(), SecurityAlgorithms.EcdsaSha512);
		var token = new JwtSecurityToken(
			issuer,
			audience,
			new[]
			{
				new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
				new Claim(ClaimTypes.Role, role),
			},
			notBefore,
			expires,
			creds);
		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	[Fact]
	public async Task ExpiredJwt_Returns401_OnProtectedEndpoint()
	{
		var jwt = CreateJwt(UserRole.GlobalRoleNames.User, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
		var response = await _publicFace.GetAsync("/api/pages");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task NotYetValidJwt_Returns401()
	{
		var jwt = CreateJwt(UserRole.GlobalRoleNames.User, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
		var response = await _publicFace.GetAsync("/api/pages");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task MalformedJwt_Returns401()
	{
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
		var response = await _publicFace.GetAsync("/api/pages");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
