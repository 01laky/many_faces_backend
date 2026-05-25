using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Routing and enforcement edge cases that must not rely on the default face-prefix test client.
/// </summary>
public class FaceScopeRoutingTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public FaceScopeRoutingTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task BareApiUsersPath_Returns400_WithGuidance()
	{
		using var client = _factory.CreateUnscopedClient();
		var response = await client.GetAsync("/api/users");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("Face URL prefix is required");
	}

	[Fact]
	public async Task OAuth2Token_StillWorks_WithoutFacePrefix()
	{
		using var client = _factory.CreateUnscopedClient();
		var response = await client.PostAsJsonAsync("/api/oauth2/token", new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = IntegrationTestSeed.Email,
			Password = IntegrationTestSeed.Password,
		});
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task PrefixedPublicApi_Albums_RequiresAuth()
	{
		using var client = _factory.CreateClient();
		var response = await client.GetAsync("/api/albums");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
