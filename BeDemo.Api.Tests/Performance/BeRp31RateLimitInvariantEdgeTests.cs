using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP31 edge cases (BE-RP31-U1…U2) — rate limiting invariant on cache-hit paths.</summary>
public sealed class BeRp31LocalizationRateLimitEdgeTests : IClassFixture<RateLimitedLocalizationWebApplicationFactory>, IDisposable
{
	private readonly HttpClient _client;

	public BeRp31LocalizationRateLimitEdgeTests(RateLimitedLocalizationWebApplicationFactory factory) =>
		_client = factory.CreateUnscopedClient();

	/// <summary>BE-RP31-U1 — localization burst returns 429 + Retry-After even when bundle is cacheable.</summary>
	[Fact]
	public async Task BE_RP31_U1_LocalizationBurst_Returns429WithRetryAfter()
	{
		(await _client.GetAsync("/api/localization/portal")).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync("/api/localization/admin")).StatusCode.Should().Be(HttpStatusCode.OK);

		var limited = await _client.GetAsync("/api/localization/mobile");
		limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
		limited.Headers.RetryAfter.Should().NotBeNull();
	}

	public void Dispose() => _client.Dispose();
}

[Trait("Category", "BackendSecurity")]
public sealed class BeRp31OAuthRateLimitEdgeTests : IClassFixture<RateLimitedOAuthWebApplicationFactory>, IDisposable
{
	private readonly HttpClient _client;

	public BeRp31OAuthRateLimitEdgeTests(RateLimitedOAuthWebApplicationFactory factory) =>
		_client = factory.CreateUnscopedClient();

	/// <summary>BE-RP31-U2 — OAuth token endpoint returns 429 after burst.</summary>
	[Fact]
	public async Task BE_RP31_U2_OAuthTokenBurst_Returns429()
	{
		for (var i = 0; i < 2; i++)
		{
			(await _client.PostAsJsonAsync("/api/oauth2/token", new OAuth2TokenRequest
			{
				GrantType = "password",
				ClientId = "be-demo-client",
				ClientSecret = "be-demo-secret-very-strong-key",
				Username = $"rp31_{Guid.NewGuid():N}@test.com",
				Password = "bad",
			})).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		(await _client.PostAsJsonAsync("/api/oauth2/token", new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = $"rp31_{Guid.NewGuid():N}@test.com",
			Password = "bad",
		})).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
	}

	public void Dispose() => _client.Dispose();
}
