namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Result of a single bundle index load via Redis cache-aside (stage 1).</summary>
public sealed record OperatorAiBundleRedisLoadResult(
	bool Success,
	string? Json,
	bool CacheHit);

/// <summary>
/// Redis L2 cache for stage 1 entity bundle JSON (per catalog index).
/// In-memory <see cref="OperatorAiBundleCacheEntry"/> remains per-request only.
/// </summary>
public interface IOperatorAiBundleRedisCache
{
	/// <summary>True when backed by Redis; false for <see cref="NoOpOperatorAiBundleRedisCache"/>.</summary>
	bool IsRedisBacked { get; }

	/// <summary>Reset per-request degraded logging flag before parallel prefetch.</summary>
	void BeginPrefetchRequest();

	Task<OperatorAiBundleRedisLoadResult> GetOrLoadAsync(
		int index,
		long ttlMs,
		Func<CancellationToken, Task<string>> loadJsonAsync,
		TimeSpan waitTimeout,
		CancellationToken cancellationToken = default);
}
