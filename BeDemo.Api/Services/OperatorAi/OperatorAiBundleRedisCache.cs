using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Redis cache-aside for live stats stage 1 bundle JSON with single-flight on miss.
/// Key namespace: <c>bedemo:operator-ai:live-bundle:v{version}:idx:{index}</c> — separate from job queue keys.
/// </summary>
public sealed class OperatorAiBundleRedisCache : IOperatorAiBundleRedisCache
{
	private readonly IOperatorAiRedisStringStore _redis;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiBundleRedisCache> _logger;
	private int _degradedLogged;

	public OperatorAiBundleRedisCache(
		IOperatorAiRedisStringStore redis,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiBundleRedisCache> logger)
	{
		_redis = redis;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public bool IsRedisBacked => true;

	/// <inheritdoc />
	public void BeginPrefetchRequest() => _degradedLogged = 0;

	/// <inheritdoc />
	public async Task<OperatorAiBundleRedisLoadResult> GetOrLoadAsync(
		int index,
		long ttlMs,
		Func<CancellationToken, Task<string>> loadJsonAsync,
		TimeSpan waitTimeout,
		CancellationToken cancellationToken = default)
	{
		var catalogVersion = OperatorAiEntityBundleCatalog.CatalogVersion;
		var bundleKey = BuildBundleKey(catalogVersion, index);
		var lockKey = BuildLockKey(catalogVersion, index);

		try
		{
			var cached = await _redis.GetAsync(bundleKey, cancellationToken);
			if (!string.IsNullOrEmpty(cached))
				return new OperatorAiBundleRedisLoadResult(true, cached, CacheHit: true);

			var lockToken = Guid.NewGuid().ToString("N");
			var lockSeconds = Math.Max(5, _options.LiveBundleCacheLockSeconds);
			var acquired = await _redis.SetNotExistsAsync(lockKey, lockToken, lockSeconds, cancellationToken);

			if (acquired)
			{
				try
				{
					// Another writer may have finished while we waited for the lock.
					cached = await _redis.GetAsync(bundleKey, cancellationToken);
					if (!string.IsNullOrEmpty(cached))
						return new OperatorAiBundleRedisLoadResult(true, cached, CacheHit: true);

					var json = await loadJsonAsync(cancellationToken);
					await _redis.SetWithExpiryMillisecondsAsync(bundleKey, json, ttlMs, cancellationToken);
					return new OperatorAiBundleRedisLoadResult(true, json, CacheHit: false);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					_logger.LogDebug(ex, "Bundle cache load failed for index {Index}; not caching failure", index);
					return new OperatorAiBundleRedisLoadResult(false, null, CacheHit: false);
				}
				finally
				{
					try
					{
						await _redis.CompareAndDeleteAsync(lockKey, lockToken, CancellationToken.None);
					}
					catch (Exception unlockEx)
					{
						_logger.LogDebug(unlockEx, "Failed to release bundle lock for index {Index}", index);
					}
				}
			}

			var pollMs = Math.Max(10, _options.LiveBundleCacheWaitPollMilliseconds);
			var deadline = DateTime.UtcNow.Add(waitTimeout);
			while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
			{
				cached = await _redis.GetAsync(bundleKey, cancellationToken);
				if (!string.IsNullOrEmpty(cached))
					return new OperatorAiBundleRedisLoadResult(true, cached, CacheHit: true);

				await Task.Delay(pollMs, cancellationToken);
			}

			return new OperatorAiBundleRedisLoadResult(false, null, CacheHit: false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogDegradedOnce(ex);
			try
			{
				var json = await loadJsonAsync(cancellationToken);
				return new OperatorAiBundleRedisLoadResult(true, json, CacheHit: false);
			}
			catch (Exception loadEx) when (loadEx is not OperationCanceledException)
			{
				return new OperatorAiBundleRedisLoadResult(false, null, CacheHit: false);
			}
		}
	}

	internal static string BuildBundleKey(int catalogVersion, int index) =>
		$"bedemo:operator-ai:live-bundle:v{catalogVersion}:idx:{index}";

	internal static string BuildLockKey(int catalogVersion, int index) =>
		$"bedemo:operator-ai:live-bundle:lock:v{catalogVersion}:idx:{index}";

	private void LogDegradedOnce(Exception ex)
	{
		if (Interlocked.CompareExchange(ref _degradedLogged, 1, 0) == 0)
		{
			_logger.LogWarning(
				ex,
				"Redis live stats bundle cache unavailable; falling back to direct DB load for this prefetch");
		}
	}
}
