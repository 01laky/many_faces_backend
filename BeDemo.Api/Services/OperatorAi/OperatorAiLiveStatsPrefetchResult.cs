namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stage 1 prefetch outcome for one live stats request (or startup warm).</summary>
public sealed record OperatorAiLiveStatsPrefetchResult(
	IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> Entries,
	int ReadyCount,
	int FailedCount,
	int CacheHits,
	int CacheMisses);
