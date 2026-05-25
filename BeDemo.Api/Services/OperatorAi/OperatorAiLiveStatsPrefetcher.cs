using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stage 1 — prefetch all 61 entity bundles (Redis L2 + DB on miss).</summary>
public interface IOperatorAiLiveStatsPrefetcher
{
	Task<OperatorAiLiveStatsPrefetchResult> PrefetchAllAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiLiveStatsPrefetcher : IOperatorAiLiveStatsPrefetcher
{
	private readonly IOperatorAiEntityBundleLoader _loader;
	private readonly IOperatorAiBundleRedisCache _cache;
	private readonly IOperatorAiLiveStatsCacheSettingsProvider _cacheSettings;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiLiveStatsPrefetcher> _logger;

	public OperatorAiLiveStatsPrefetcher(
		IOperatorAiEntityBundleLoader loader,
		IOperatorAiBundleRedisCache cache,
		IOperatorAiLiveStatsCacheSettingsProvider cacheSettings,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiLiveStatsPrefetcher> logger)
	{
		_loader = loader;
		_cache = cache;
		_cacheSettings = cacheSettings;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<OperatorAiLiveStatsPrefetchResult> PrefetchAllAsync(
		CancellationToken cancellationToken = default)
	{
		var started = DateTime.UtcNow;
		var entries = new Dictionary<int, OperatorAiBundleCacheEntry>(OperatorAiEntityBundleCatalog.BundleCount);
		var timeout = TimeSpan.FromSeconds(_options.LivePrefetchTimeoutSeconds);
		var ttlMs = await _cacheSettings.GetTtlMillisecondsAsync(cancellationToken);

		_cache.BeginPrefetchRequest();

		var hits = 0;
		var misses = 0;
		var statsLock = new object();

		var tasks = Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount)
			.Select(index => PrefetchOneAsync(
				index,
				entries,
				ttlMs,
				timeout,
				() =>
				{
					lock (statsLock)
					{
						hits++;
					}
				},
				() =>
				{
					lock (statsLock)
					{
						misses++;
					}
				},
				cancellationToken))
			.ToArray();

		await Task.WhenAll(tasks);

		var ready = entries.Values.Count(e => e.State == OperatorAiBundleCacheState.Ready);
		var failed = entries.Values.Count(e => e.State == OperatorAiBundleCacheState.Failed);

		_logger.LogInformation(
			"Live stats prefetch completed in {ElapsedMs}ms ({Ready} ready, {Failed} failed, cacheHits={CacheHits}, cacheMisses={CacheMisses})",
			(DateTime.UtcNow - started).TotalMilliseconds,
			ready,
			failed,
			hits,
			misses);

		return new OperatorAiLiveStatsPrefetchResult(entries, ready, failed, hits, misses);
	}

	private async Task PrefetchOneAsync(
		int index,
		Dictionary<int, OperatorAiBundleCacheEntry> entries,
		long ttlMs,
		TimeSpan timeout,
		Action onCacheHit,
		Action onCacheMiss,
		CancellationToken cancellationToken)
	{
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
		var startedUtc = DateTime.UtcNow;
		entries[index] = new OperatorAiBundleCacheEntry(
			index,
			meta.Id,
			OperatorAiBundleCacheState.Loading,
			null,
			null,
			startedUtc,
			null);

		try
		{
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			linked.CancelAfter(timeout);

			var loadResult = await _cache.GetOrLoadAsync(
				index,
				ttlMs,
				async ct =>
				{
					var dto = await _loader.LoadAsync(index, ct);
					return OperatorAiEntityBundleLoader.Serialize(dto);
				},
				timeout,
				linked.Token);

			if (loadResult.CacheHit)
				onCacheHit();
			else if (loadResult.Success)
				onCacheMiss();

			if (!loadResult.Success || string.IsNullOrEmpty(loadResult.Json))
			{
				entries[index] = entries[index] with
				{
					State = OperatorAiBundleCacheState.Failed,
					ErrorMessage = "Prefetch timeout",
					CompletedUtc = DateTime.UtcNow,
				};
				return;
			}

			entries[index] = entries[index] with
			{
				State = OperatorAiBundleCacheState.Ready,
				JsonPayload = loadResult.Json,
				CompletedUtc = DateTime.UtcNow,
			};
		}
		catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
		{
			entries[index] = entries[index] with
			{
				State = OperatorAiBundleCacheState.Failed,
				ErrorMessage = "Prefetch timeout",
				CompletedUtc = DateTime.UtcNow,
			};
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Bundle prefetch failed for index {Index} ({BundleId})", index, meta.Id);
			entries[index] = entries[index] with
			{
				State = OperatorAiBundleCacheState.Failed,
				ErrorMessage = ex.Message,
				CompletedUtc = DateTime.UtcNow,
			};
		}
	}
}
