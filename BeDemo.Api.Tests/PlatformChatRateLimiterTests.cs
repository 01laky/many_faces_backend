using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class PlatformChatRateLimiterTests
{
	[Fact]
	public void TryAllow_ShouldBlockAfterMaxPerWindow()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["MessengerHub:PlatformChatMaxPerWindow"] = "2",
				["MessengerHub:PlatformChatWindowSeconds"] = "60",
			})
			.Build();
		var limiter = new PlatformChatRateLimiter(new MemoryCache(new MemoryCacheOptions()), config);
		limiter.TryAllow("op1").Should().BeTrue();
		limiter.TryAllow("op1").Should().BeTrue();
		limiter.TryAllow("op1").Should().BeFalse();
	}
}
