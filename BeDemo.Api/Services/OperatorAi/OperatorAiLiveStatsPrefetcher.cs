using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stage 1 — prefetch entity bundles (Redis L2 + DB on miss).</summary>
public interface IOperatorAiLiveStatsPrefetcher
{
	/// <summary>Prefetch all 61 bundles (legacy broad-overview / planner path).</summary>
	Task<OperatorAiLiveStatsPrefetchResult> PrefetchAllAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// RAG path (§3.3): fresh-load ONLY the retrieved top-K bundle indices — never the always-all-61 prefetch.
	/// Values are loaded from the same Redis L2 → Postgres loaders as <see cref="PrefetchAllAsync"/>, so counts
	/// stay exact (§4 rule 2). Out-of-range / duplicate indices are ignored; an empty input yields an empty map.
	/// </summary>
	Task<OperatorAiLiveStatsPrefetchResult> PrefetchSelectedAsync(
		IReadOnlyList<int> indices,
		CancellationToken cancellationToken = default);
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
	public Task<OperatorAiLiveStatsPrefetchResult> PrefetchAllAsync(
		CancellationToken cancellationToken = default) =>
		PrefetchManyAsync(
			Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount).ToArray(),
			cancellationToken);

	/// <inheritdoc />
	public Task<OperatorAiLiveStatsPrefetchResult> PrefetchSelectedAsync(
		IReadOnlyList<int> indices,
		CancellationToken cancellationToken = default)
	{
		// Dedupe + drop out-of-range indices so the loader is only hit for valid catalog bundles.
		var selected = indices
			.Where(i => i >= 0 && i < OperatorAiEntityBundleCatalog.BundleCount)
			.Distinct()
			.ToArray();
		return PrefetchManyAsync(selected, cancellationToken);
	}

	/// <summary>Shared prefetch core — loads exactly the supplied catalog indices in parallel (Redis L2 → DB).</summary>
	private async Task<OperatorAiLiveStatsPrefetchResult> PrefetchManyAsync(
		IReadOnlyList<int> indicesToLoad,
		CancellationToken cancellationToken)
	{
		var started = DateTime.UtcNow;
		// 7B-perf / backend-refactor §4.1 fix: ConcurrentDictionary — the entries map is written from parallel
		// PrefetchOneAsync tasks (Task.WhenAll), and a plain Dictionary is not thread-safe even for distinct keys.
		var entries = new ConcurrentDictionary<int, OperatorAiBundleCacheEntry>();
		var timeout = TimeSpan.FromSeconds(_options.LivePrefetchTimeoutSeconds);

		if (indicesToLoad.Count == 0)
			return new OperatorAiLiveStatsPrefetchResult(entries, 0, 0, 0, 0);

		var ttlMs = await _cacheSettings.GetTtlMillisecondsAsync(cancellationToken);

		_cache.BeginPrefetchRequest();

		var hits = 0;
		var misses = 0;
		var statsLock = new object();

		var tasks = indicesToLoad
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
		ConcurrentDictionary<int, OperatorAiBundleCacheEntry> entries,
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
