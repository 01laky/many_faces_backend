using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Extended edge-case coverage for <see cref="OperatorAiAnswerCache"/> — the optional exact-repeat answer
/// cache (O18). Covers the enabled/disabled gate, key normalization (trim / lower / whitespace-collapse),
/// skill isolation, empty-answer rejection, and the O(1) generation-based <c>Clear()</c> used on reindex.
/// </summary>
public sealed class OperatorAiAnswerCacheTests
{
	private static OperatorAiAnswerCache Cache(bool enabled = true, int ttlSeconds = 30) =>
		new(
			new MemoryCache(new MemoryCacheOptions()),
			Options.Create(new OperatorAiOptions { AnswerCacheEnabled = enabled, AnswerCacheTtlSeconds = ttlSeconds }));

	[Fact]
	public void Disabled_cache_never_stores_or_serves()
	{
		var cache = Cache(enabled: false);
		cache.Set("stats", "how many users", "1,234");
		cache.TryGet("stats", "how many users", out var hit).Should().BeFalse();
		hit.Should().BeEmpty();
	}

	[Fact]
	public void Enabled_cache_serves_an_identical_repeat_and_misses_a_different_question()
	{
		var cache = Cache();
		cache.Set("stats", "how many users", "1,234");

		cache.TryGet("stats", "how many users", out var hit).Should().BeTrue();
		hit.Should().Be("1,234");
		cache.TryGet("stats", "how many albums", out _).Should().BeFalse("a different question misses");
	}

	[Fact]
	public void Keys_are_isolated_per_skill()
	{
		var cache = Cache();
		cache.Set("stats", "count", "stats-answer");

		cache.TryGet("reports", "count", out _).Should().BeFalse("the same message under another skill must miss");
		cache.TryGet("stats", "count", out var hit).Should().BeTrue();
		hit.Should().Be("stats-answer");
	}

	[Theory]
	[InlineData("How Many Users", "how many users")] // case-insensitive
	[InlineData("how many users", "HOW MANY USERS")] // case-insensitive (reverse)
	[InlineData("how   many    users", "how many users")] // collapse internal whitespace
	[InlineData("  how many users  ", "how many users")] // trim ends
	[InlineData("how\tmany\nusers", "how many users")] // tabs/newlines normalize to single space
	public void Message_key_is_normalized(string setMessage, string getMessage)
	{
		var cache = Cache();
		cache.Set("stats", setMessage, "answer");
		cache.TryGet("stats", getMessage, out var hit).Should().BeTrue();
		hit.Should().Be("answer");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\n")]
	public void Empty_or_whitespace_answers_are_not_cached(string answer)
	{
		var cache = Cache();
		cache.Set("stats", "how many users", answer);
		cache.TryGet("stats", "how many users", out _).Should().BeFalse("a blank answer must never be cached");
	}

	[Fact]
	public void Clear_invalidates_everything_and_the_cache_is_reusable_afterwards()
	{
		var cache = Cache();
		cache.Set("stats", "a", "1");
		cache.Set("reports", "b", "2");

		cache.Clear();

		cache.TryGet("stats", "a", out _).Should().BeFalse("Clear() (reindex) drops every entry");
		cache.TryGet("reports", "b", out _).Should().BeFalse();

		// The generation bump must not break future caching.
		cache.Set("stats", "a", "3");
		cache.TryGet("stats", "a", out var hit).Should().BeTrue();
		hit.Should().Be("3");
	}

	[Fact]
	public void Repeated_clears_keep_invalidating()
	{
		var cache = Cache();
		for (var i = 0; i < 5; i++)
		{
			cache.Set("stats", "q", $"answer-{i}");
			cache.TryGet("stats", "q", out var hit).Should().BeTrue();
			hit.Should().Be($"answer-{i}");
			cache.Clear();
			cache.TryGet("stats", "q", out _).Should().BeFalse();
		}
	}

	[Fact]
	public void Zero_or_negative_ttl_is_clamped_to_at_least_one_second_and_still_caches()
	{
		var cache = Cache(ttlSeconds: 0);
		cache.Set("stats", "q", "ok");
		cache.TryGet("stats", "q", out var hit).Should().BeTrue("TTL is clamped to a minimum of 1s, not 0");
		hit.Should().Be("ok");
	}
}
