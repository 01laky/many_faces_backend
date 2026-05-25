using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BeDemo.Api.Tests;

public sealed class ChatHubAiRateLimiterTests
{
	private static ChatHubAiRateLimiter CreateSut(IConfiguration config)
	{
		return new ChatHubAiRateLimiter(new MemoryCache(new MemoryCacheOptions()), config);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void TryAllow_ShouldReject_WhenUserIdMissing(string? userId)
	{
		var config = new ConfigurationBuilder().Build();
		var sut = CreateSut(config);
		sut.TryAllow(userId).Should().BeFalse();
	}

	[Fact]
	public void TryAllow_ShouldTreatWhitespaceUserId_AsDistinctKey()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["ChatHub:SendAiMaxPerWindow"] = "1" })
			.Build();
		var sut = CreateSut(config);
		sut.TryAllow("   ").Should().BeTrue();
		sut.TryAllow("   ").Should().BeFalse();
	}

	[Fact]
	public void TryAllow_ShouldAlwaysAllow_WhenMaxPerWindowIsZero()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ChatHub:SendAiMaxPerWindow"] = "0",
			})
			.Build();
		var sut = CreateSut(config);
		for (var i = 0; i < 5; i++)
			sut.TryAllow("user-1").Should().BeTrue();
	}

	[Fact]
	public void TryAllow_ShouldDenyAfterBudgetExhausted()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ChatHub:SendAiMaxPerWindow"] = "2",
				["ChatHub:SendAiWindowSeconds"] = "60",
			})
			.Build();
		var sut = CreateSut(config);

		sut.TryAllow("user-a").Should().BeTrue();
		sut.TryAllow("user-a").Should().BeTrue();
		sut.TryAllow("user-a").Should().BeFalse();
	}

	[Fact]
	public void TryAllow_ShouldIsolateCountersPerUser()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ChatHub:SendAiMaxPerWindow"] = "1",
			})
			.Build();
		var sut = CreateSut(config);

		sut.TryAllow("user-a").Should().BeTrue();
		sut.TryAllow("user-b").Should().BeTrue();
		sut.TryAllow("user-a").Should().BeFalse();
	}
}
