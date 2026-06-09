using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BeDemo.Api.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// 7B-perf O17 — single-active-generation guard. The local GPU is a serial resource: if an operator fires a second
/// question while the first 90–120s turn is still generating, both turns thrash the GPU and BOTH get slower. This
/// keyed in-memory guard lets the ChatHub reject a second concurrent turn for the same conversation with a friendly
/// message until the first completes. Independent of the request-rate limiter (which counts requests, not in-flight
/// generations). Singleton (in-process, single-node dev).
/// </summary>
public interface IOperatorAiActiveGenerationGuard
{
	/// <summary>Reserve the conversation for a turn; false when one is already generating.</summary>
	bool TryBegin(int conversationId);

	/// <summary>Release the conversation when the turn finishes (success or failure).</summary>
	void End(int conversationId);
}

/// <inheritdoc />
public sealed class OperatorAiActiveGenerationGuard : IOperatorAiActiveGenerationGuard
{
	private readonly ConcurrentDictionary<int, byte> _active = new();

	public bool TryBegin(int conversationId) => _active.TryAdd(conversationId, 1);

	public void End(int conversationId) => _active.TryRemove(conversationId, out _);
}

/// <summary>
/// 7B-perf O18 — optional short-TTL exact-repeat answer cache. Operators often re-ask the same thing; when enabled,
/// an identical question (same skill + normalized text) within a short TTL returns the cached answer with 0
/// generations. Off by default. The TTL MUST stay ≤ the bundle-cache freshness window so a cached count never
/// outlives the live data; the cache is also flushed on knowledge reindex. Singleton over IMemoryCache.
/// </summary>
public interface IOperatorAiAnswerCache
{
	/// <summary>True + the cached answer when enabled and a fresh identical (skill, message) entry exists.</summary>
	bool TryGet(string skillId, string message, out string answer);

	/// <summary>Cache the answer for a turn (no-op when disabled or the answer is empty).</summary>
	void Set(string skillId, string message, string answer);

	/// <summary>Drop all cached answers (e.g. after a knowledge reindex) so stale data can't be served.</summary>
	void Clear();
}

/// <inheritdoc />
public sealed class OperatorAiAnswerCache : IOperatorAiAnswerCache
{
	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

	private readonly IMemoryCache _cache;
	private readonly OperatorAiOptions _options;
	// A generation token lets Clear() invalidate every entry in O(1) without enumerating the shared IMemoryCache.
	private long _generation;

	public OperatorAiAnswerCache(IMemoryCache cache, IOptions<OperatorAiOptions> options)
	{
		_cache = cache;
		_options = options.Value;
	}

	public bool TryGet(string skillId, string message, out string answer)
	{
		answer = string.Empty;
		if (!_options.AnswerCacheEnabled)
			return false;

		if (_cache.TryGetValue<string>(Key(skillId, message), out var cached) && !string.IsNullOrEmpty(cached))
		{
			answer = cached!;
			return true;
		}
		return false;
	}

	public void Set(string skillId, string message, string answer)
	{
		if (!_options.AnswerCacheEnabled || string.IsNullOrWhiteSpace(answer))
			return;

		_cache.Set(
			Key(skillId, message),
			answer,
			new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.AnswerCacheTtlSeconds)),
				Size = 1,
			});
	}

	public void Clear() => Interlocked.Increment(ref _generation);

	private string Key(string skillId, string message)
	{
		var normalized = Whitespace.Replace((message ?? string.Empty).Trim().ToLowerInvariant(), " ");
		return $"operator-ai:answer:{Interlocked.Read(ref _generation)}:{skillId}:{normalized}";
	}
}
