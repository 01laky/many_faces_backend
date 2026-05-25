using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// OAuth and JWKS live under <c>/api/oauth2/*</c> without face prefix; routing must not require <c>/{face}/</c> for these paths (prompt §17.3).
/// </summary>
public sealed class OAuthExemptPathRegressionTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public OAuthExemptPathRegressionTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Jwks_OnUnscopedClient_Returns200_Not404()
	{
		using var client = _factory.CreateUnscopedClient();
		var res = await client.GetAsync("/api/oauth2/jwks");
		res.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		res.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Token_OnUnscopedClient_ReachesOAuth_Not404()
	{
		using var client = _factory.CreateUnscopedClient();
		var res = await client.PostAsJsonAsync(
			"/api/oauth2/token",
			new OAuth2TokenRequest
			{
				GrantType = "password",
				ClientId = "be-demo-client",
				ClientSecret = "wrong",
				Username = "nobody@test.com",
				Password = "x",
			});
		res.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
