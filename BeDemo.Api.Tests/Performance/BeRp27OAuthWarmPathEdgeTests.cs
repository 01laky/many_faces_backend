using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP27 edge cases (BE-RP27-U1…U5).</summary>
public sealed class BeRp27OAuthWarmPathEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public BeRp27OAuthWarmPathEdgeTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = factory.CreateUnscopedClient();
	}

	/// <summary>BE-RP27-U1 — password grant returns tokens for valid client + user.</summary>
	[Fact]
	public async Task BE_RP27_U1_PasswordGrant_ValidCredentials_ReturnsTokens()
	{
		var email = $"rp27_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");

		var response = await PostTokenAsync(new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "Test1234!@##",
		});

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		body!.AccessToken.Should().NotBeNullOrEmpty();
		body.RefreshToken.Should().NotBeNullOrEmpty();
		body.ExpiresIn.Should().BeGreaterThan(0);
	}

	/// <summary>BE-RP27-U2 — refresh grant rotates tokens; old refresh rejected.</summary>
	[Fact]
	public async Task BE_RP27_U2_RefreshGrant_Rotates_OldRefreshRejected()
	{
		var email = $"rp27_ref_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var initial = await PostTokenAsync(PasswordRequest(email));
		var initialBody = await initial.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		var refresh = await PostTokenAsync(new OAuth2TokenRequest
		{
			GrantType = "refresh_token",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			RefreshToken = initialBody!.RefreshToken,
		});
		refresh.StatusCode.Should().Be(HttpStatusCode.OK);
		var rotated = await refresh.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		var reuse = await PostTokenAsync(new OAuth2TokenRequest
		{
			GrantType = "refresh_token",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			RefreshToken = initialBody.RefreshToken,
		});
		reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		rotated!.AccessToken.Should().NotBe(initialBody.AccessToken);
	}

	/// <summary>BE-RP27-U3 — invalid client returns 401.</summary>
	[Fact]
	public async Task BE_RP27_U3_InvalidClient_ReturnsUnauthorized()
	{
		var response = await PostTokenAsync(new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "wrong-secret",
			Username = "nobody@test.com",
			Password = "x",
		});
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>BE-RP27-U4 — rate limit still returns 429 (cross-check BE-RP31).</summary>
	[Fact]
	public async Task BE_RP27_U4_RateLimitStillEnforced_WithRateLimitedFactory()
	{
		using var factory = new RateLimitedOAuthWebApplicationFactory();
		using var client = factory.CreateUnscopedClient();
		for (var i = 0; i < 2; i++)
		{
			(await client.PostAsJsonAsync("/api/oauth2/token", PasswordRequest($"rp27_rl_{Guid.NewGuid():N}@test.com")))
				.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "invalid user still counts toward limiter partition");
		}

		(await client.PostAsJsonAsync("/api/oauth2/token", PasswordRequest($"rp27_rl_{Guid.NewGuid():N}@test.com")))
			.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
	}

	/// <summary>BE-RP27-U5 — access token includes atv claim for BE-RP1 compatibility.</summary>
	[Fact]
	public async Task BE_RP27_U5_AccessToken_ContainsAtvClaim()
	{
		var email = $"rp27_atv_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var response = await PostTokenAsync(PasswordRequest(email));
		var body = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);
		jwt.Claims.Should().Contain(c => c.Type == "atv");
		jwt.Claims.First(c => c.Type == "atv").Value.Should().NotBeNullOrEmpty();
	}

	private static OAuth2TokenRequest PasswordRequest(string email) => new()
	{
		GrantType = "password",
		ClientId = "be-demo-client",
		ClientSecret = "be-demo-secret-very-strong-key",
		Username = email,
		Password = "Test1234!@##",
	};

	private async Task<HttpResponseMessage> PostTokenAsync(OAuth2TokenRequest request)
	{
		HttpResponseMessage? response = null;
		for (var i = 0; i < 10; i++)
		{
			await Task.Delay(100 * (i + 1));
			response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
			if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests)
				break;
		}

		return response!;
	}
}
