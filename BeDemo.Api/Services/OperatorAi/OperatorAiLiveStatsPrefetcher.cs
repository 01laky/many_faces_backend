using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stage 1 — prefetch all 61 entity bundles into an in-memory cache (DB only, not LLM).</summary>
public interface IOperatorAiLiveStatsPrefetcher
{
    Task<IReadOnlyDictionary<int, OperatorAiBundleCacheEntry>> PrefetchAllAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiLiveStatsPrefetcher : IOperatorAiLiveStatsPrefetcher
{
    private readonly IOperatorAiEntityBundleLoader _loader;
    private readonly OperatorAiOptions _options;
    private readonly ILogger<OperatorAiLiveStatsPrefetcher> _logger;

    public OperatorAiLiveStatsPrefetcher(
        IOperatorAiEntityBundleLoader loader,
        IOptions<OperatorAiOptions> options,
        ILogger<OperatorAiLiveStatsPrefetcher> logger)
    {
        _loader = loader;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, OperatorAiBundleCacheEntry>> PrefetchAllAsync(
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var entries = new Dictionary<int, OperatorAiBundleCacheEntry>(OperatorAiEntityBundleCatalog.BundleCount);
        var timeout = TimeSpan.FromSeconds(_options.LivePrefetchTimeoutSeconds);

        var tasks = Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount)
            .Select(index => PrefetchOneAsync(index, entries, timeout, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Live stats prefetch completed in {ElapsedMs}ms ({Ready} ready, {Failed} failed)",
            (DateTime.UtcNow - started).TotalMilliseconds,
            entries.Values.Count(e => e.State == OperatorAiBundleCacheState.Ready),
            entries.Values.Count(e => e.State == OperatorAiBundleCacheState.Failed));

        return entries;
    }

    private async Task PrefetchOneAsync(
        int index,
        Dictionary<int, OperatorAiBundleCacheEntry> entries,
        TimeSpan timeout,
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
            var dto = await _loader.LoadAsync(index, linked.Token);
            var json = OperatorAiEntityBundleLoader.Serialize(dto);
            entries[index] = entries[index] with
            {
                State = OperatorAiBundleCacheState.Ready,
                JsonPayload = json,
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
