using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Optional stale-answer fallback (config-gated, default off) — the answer cache retains a longer-lived stale copy,
/// independent of the fresh exact-repeat cache, that `TryGetStale` returns so the hub can serve the last successful
/// answer (with a "may be stale" note) when the AI is down.
/// </summary>
public sealed class OperatorAiAnswerCacheStaleTests
{
	private static OperatorAiAnswerCache Build(OperatorAiOptions opts) =>
		new(new MemoryCache(new MemoryCacheOptions()), Options.Create(opts));

	[Fact]
	public void TryGetStale_is_false_when_the_fallback_is_disabled()
	{
		var cache = Build(new OperatorAiOptions { AnswerCacheEnabled = true });
		cache.Set("stats", "how many users?", "There are 33 users.");
		cache.TryGetStale("stats", "how many users?", out var answer).Should().BeFalse("the stale fallback is off by default");
		answer.Should().BeEmpty();
	}

	[Fact]
	public void Stale_copy_is_retained_independently_of_the_fresh_cache_and_normalizes_the_key()
	{
		// Fresh exact-repeat cache OFF, stale fallback ON — Set still retains a stale copy, and the (skill, message)
		// key is normalized (lowercase + collapsed whitespace) the same way as the fresh cache.
		var cache = Build(new OperatorAiOptions { AnswerCacheEnabled = false, StaleAnswerFallbackEnabled = true, StaleAnswerTtlSeconds = 60 });
		cache.Set("stats", "How many   USERS?", "There are 33 users.");

		cache.TryGet("stats", "how many users?", out _).Should().BeFalse("the fresh cache is disabled");
		cache.TryGetStale("stats", "how many users?", out var stale).Should().BeTrue();
		stale.Should().Be("There are 33 users.");
	}

	[Fact]
	public void Clear_invalidates_the_stale_copy()
	{
		var cache = Build(new OperatorAiOptions { StaleAnswerFallbackEnabled = true, StaleAnswerTtlSeconds = 60 });
		cache.Set("stats", "q", "answer");
		cache.Clear();
		cache.TryGetStale("stats", "q", out _).Should().BeFalse("Clear bumps the generation token so old keys miss (no stale data after a reindex)");
	}
}
