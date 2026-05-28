using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP22 — hot read path latency smoke tests.</summary>
public sealed class HotPathPerformanceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _publicFace;
	private readonly HttpClient _profileClient;

	public HotPathPerformanceTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
		_profileClient = factory.CreateUnscopedClient();
	}

	private static long PercentileMs(List<long> sortedMs, int p)
	{
		if (sortedMs.Count == 0) return 0;
		var idx = (int)Math.Ceiling(p / 100.0 * sortedMs.Count) - 1;
		idx = Math.Clamp(idx, 0, sortedMs.Count - 1);
		return sortedMs[idx];
	}

	/// <summary>BE-RP22-U1 — 50 sequential faces config requests stay under p95 threshold.</summary>
	[Fact]
	public async Task BE_RP22_U1_FacesConfig_50Sequential_P95UnderThreshold()
	{
		const int iterations = 50;
		const long p95ThresholdMs = 3000;
		var times = new List<long>(iterations);

		for (var i = 0; i < iterations; i++)
		{
			var sw = Stopwatch.StartNew();
			var response = await _publicFace.GetAsync("/api/faces/config");
			sw.Stop();
			response.StatusCode.Should().Be(HttpStatusCode.OK);
			times.Add(sw.ElapsedMilliseconds);
		}

		times.Sort();
		var p95 = PercentileMs(times, 95);
		p95.Should().BeLessThan(p95ThresholdMs,
			$"faces config p95={p95}ms across {iterations} iterations (warm cache expected after first hit)");
	}

	/// <summary>BE-RP22-U2 — 100 sequential profile/me with warm JWT cache.</summary>
	[Fact]
	public async Task BE_RP22_U2_ProfileMe_100SequentialWithWarmJwt_P95UnderThreshold()
	{
		const int iterations = 100;
		const long p95ThresholdMs = 2500;
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_profileClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		// Warm JWT + ATV cache
		(await _profileClient.GetAsync("/api/profile/me")).EnsureSuccessStatusCode();

		var times = new List<long>(iterations);
		for (var i = 0; i < iterations; i++)
		{
			var sw = Stopwatch.StartNew();
			var response = await _profileClient.GetAsync("/api/profile/me");
			sw.Stop();
			response.StatusCode.Should().Be(HttpStatusCode.OK);
			times.Add(sw.ElapsedMilliseconds);
		}

		times.Sort();
		var p95 = PercentileMs(times, 95);
		p95.Should().BeLessThan(p95ThresholdMs,
			$"profile/me p95={p95}ms with warm JWT cache across {iterations} iterations");
	}
}
