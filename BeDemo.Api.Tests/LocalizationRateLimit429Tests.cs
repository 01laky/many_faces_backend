using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Verifies the <c>localization-read</c> ASP.NET rate-limit policy on <c>GET /api/localization/{app}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Static UI bundles are fetched anonymously on every cold load (portal, admin, mobile). Without a dedicated
/// limit, an attacker could scrape all three apps in a tight loop. Production defaults are generous
/// (<see cref="RateLimitedLocalizationWebApplicationFactory"/> uses a low permit count only for this test host).
/// </para>
/// <para>
/// Rejection uses the global <see cref="Program"/> rate-limiter <c>OnRejected</c> handler: HTTP 429,
/// <c>Retry-After</c> header, and JSON <c>{"error":"rate_limit",...}</c>.
/// </para>
/// <para>
/// All scenarios run in a single test method so the shared <see cref="RateLimitedLocalizationWebApplicationFactory"/>
/// host is not left in a depleted window for a follow-up test (xUnit does not guarantee method order).
/// </para>
/// </remarks>
[Collection(LocalizationRateLimitCollection.Name)]
public sealed class LocalizationRateLimit429Tests : IDisposable
{
	private readonly RateLimitedLocalizationWebApplicationFactory _factory;
	private readonly HttpClient _client;

	/// <summary>
	/// Receives the shared <see cref="RateLimitedLocalizationWebApplicationFactory"/> from
	/// <see cref="LocalizationRateLimitCollection"/> (one host per collection).
	/// </summary>
	public LocalizationRateLimit429Tests(RateLimitedLocalizationWebApplicationFactory factory)
	{
		_factory = factory;
		// Localization is face-prefix exempt — use an unscoped client (bare /api/localization/...).
		_client = factory.CreateUnscopedClient();
	}

	/// <summary>
	/// End-to-end: host config, shared IP partition across apps, burst 429 body/headers, window reset.
	/// </summary>
	[Fact]
	public async Task Localization_read_policy_enforces_429_with_retry_after_and_shared_ip_partition()
	{
		using (var scope = _factory.Services.CreateScope())
		{
			var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
			config["Testing:RateLimitScopeId"].Should().NotBeNullOrEmpty();
			config.GetValue<bool>("OAuth2:BypassRateLimitInTesting", true).Should().BeFalse();
			config.GetValue<int>("Localization:RateLimitPermitLimit", 0).Should().Be(2);
		}

		// Phase A — portal + admin consume two permits; mobile is rejected (same IP partition, not per-app).
		(await _client.GetAsync("/api/localization/portal")).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync("/api/localization/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync("/api/localization/mobile")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

		// Phase B — after the 3s test window, burst again on portal and assert JSON 429 shape.
		await Task.Delay(TimeSpan.FromSeconds(3.5));

		(await _client.GetAsync("/api/localization/portal")).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync("/api/localization/portal")).StatusCode.Should().Be(HttpStatusCode.OK);

		var limited = await _client.GetAsync("/api/localization/portal");
		limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
		limited.Headers.RetryAfter.Should().NotBeNull();
		limited.Headers.RetryAfter!.Delta.Should().NotBeNull();

		limited.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
		var body = await limited.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(body);
		doc.RootElement.GetProperty("error").GetString().Should().Be("rate_limit");
		doc.RootElement.GetProperty("error_description").GetString().Should().Contain("Too many requests");
	}

	public void Dispose() => _client.Dispose();
}
