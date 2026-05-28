using BeDemo.Api.Configuration;
using BeDemo.Api.Services.Auth;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>
/// BE-RP18 edge cases — ATV uses IMemoryCache (distributed Redis waived); Operator AI bundle Redis optional.
/// </summary>
public sealed class BeRp18DistributedCacheEdgeTests
{
	/// <summary>BE-RP18-U1 — AccessTokenVersionCache is in-process memory only (no Redis seam in v1).</summary>
	[Fact]
	public void BE_RP18_U1_AccessTokenVersionCache_UsesMemoryNotRedis()
	{
		var ctor = typeof(AccessTokenVersionCache).GetConstructors().Single();
		ctor.GetParameters().Select(p => p.ParameterType).Should().Contain(typeof(IMemoryCache));
		ctor.GetParameters().Select(p => p.ParameterType.Name).Should().NotContain("IDistributedCache");
		new NoOpOperatorAiBundleRedisCache().IsRedisBacked.Should().BeFalse();
	}

	/// <summary>BE-RP18-U2 — NoOp bundle cache always loads via delegate (Redis miss path in dev/test).</summary>
	[Fact]
	public async Task BE_RP18_U2_NoOpBundleCache_AlwaysInvokesLoader()
	{
		var cache = new NoOpOperatorAiBundleRedisCache();
		var calls = 0;
		var result = await cache.GetOrLoadAsync(
			0,
			60_000,
			_ =>
			{
				calls++;
				return Task.FromResult("{\"ok\":true}");
			},
			TimeSpan.FromSeconds(1));

		result.Success.Should().BeTrue();
		result.CacheHit.Should().BeFalse();
		calls.Should().Be(1);
	}
}
