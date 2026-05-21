using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Live stats map-reduce orchestrator (stages 1–5): prefetch → planner → queued bundle AI → stitch.
/// Uses existing <see cref="IAiGrpcService.GenerateAsync"/> — Option B from the agent prompt.
/// </summary>
public interface IOperatorAiLiveStatsOrchestrator
{
    Task<string> RunAsync(
        string userMessage,
        string responseLocale,
        int maxParallelBundleAiCalls,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiLiveStatsOrchestrator : IOperatorAiLiveStatsOrchestrator
{
    private readonly IOperatorAiLiveStatsPrefetcher _prefetcher;
    private readonly IAiGrpcService _ai;
    private readonly OperatorAiOptions _options;
    private readonly ILogger<OperatorAiLiveStatsOrchestrator> _logger;

    public OperatorAiLiveStatsOrchestrator(
        IOperatorAiLiveStatsPrefetcher prefetcher,
        IAiGrpcService ai,
        IOptions<OperatorAiOptions> options,
        ILogger<OperatorAiLiveStatsOrchestrator> logger)
    {
        _prefetcher = prefetcher;
        _ai = ai;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(
        string userMessage,
        string responseLocale,
        int maxParallelBundleAiCalls,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.LiveTotalTimeoutSeconds));

        var metricsLike = OperatorAiStatsIntent.IsMetricsQuestion(userMessage);
        if (!metricsLike)
        {
            return await _ai.GenerateAsync(
                BuildPlainPrompt(userMessage),
                _options.MaxNewTokens,
                responseLocale: responseLocale,
                cancellationToken: timeoutCts.Token);
        }

        var parallel = Math.Clamp(maxParallelBundleAiCalls, 1, _options.MaxParallelBundleAiCalls);
        var catalog = OperatorAiEntityBundleCatalog.ToPlannerCatalogDto();

        // Stage 1 + 2 in parallel: prefetch all bundles while planner selects indices.
        var prefetchTask = _prefetcher.PrefetchAllAsync(timeoutCts.Token);
        var plannerPrompt = OperatorAiLiveStatsPlanner.BuildPrompt(userMessage, catalog);
        var plannerRaw = await _ai.GenerateAsync(
            plannerPrompt,
            _options.LivePlannerMaxNewTokens,
            responseLocale: "en",
            cancellationToken: timeoutCts.Token);

        var planner = OperatorAiLiveStatsPlanner.ParseIndices(
            plannerRaw,
            OperatorAiEntityBundleCatalog.BundleCount,
            _options.MaxSelectedBundleIndices,
            metricsLike);

        var cache = await prefetchTask;

        if (planner.Indices.Count == 0)
        {
            return await _ai.GenerateAsync(
                BuildPlainPrompt(userMessage),
                _options.MaxNewTokens,
                responseLocale: responseLocale,
                cancellationToken: timeoutCts.Token);
        }

        _logger.LogInformation(
            "Live planner selected indices [{Indices}] for operator question",
            string.Join(", ", planner.Indices));

        // Stage 4 — per-bundle AI with semaphore queue (max N concurrent).
        using var gate = new SemaphoreSlim(parallel, parallel);
        var partTasks = planner.Indices.Select(index => RunBundleAiAsync(
            index,
            userMessage,
            responseLocale,
            cache,
            gate,
            timeoutCts.Token)).ToArray();

        var parts = await Task.WhenAll(partTasks);
        return OperatorAiLiveStatsStitch.Stitch(parts);
    }

    private async Task<OperatorAiLiveStatsStitch.Part> RunBundleAiAsync(
        int index,
        string userMessage,
        string responseLocale,
        IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> cache,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
        if (!cache.TryGetValue(index, out var entry))
            return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);

        // Stage 3 barrier — wait until this index is ready (prefetch may still be finishing other indices).
        while (entry.State == OperatorAiBundleCacheState.Loading)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50, cancellationToken);
            if (cache.TryGetValue(index, out var updated))
                entry = updated;
            else
                return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);
        }

        if (entry.State != OperatorAiBundleCacheState.Ready || string.IsNullOrEmpty(entry.JsonPayload))
            return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var prompt = BuildBundlePrompt(userMessage, meta, entry.JsonPayload, responseLocale);
            var text = await _ai.GenerateAsync(
                prompt,
                _options.LiveBundleMaxNewTokens,
                responseLocale: responseLocale,
                cancellationToken: cancellationToken);
            return new OperatorAiLiveStatsStitch.Part(index, meta.Id, text ?? string.Empty, Failed: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bundle AI failed for index {Index}", index);
            return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildBundlePrompt(
        string userMessage,
        OperatorAiBundleCatalogEntryDto meta,
        string bundleJson,
        string responseLocale)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Bundle task — facts only, no fluff]");
        sb.AppendLine($"Bundle id: {meta.Id}");
        sb.AppendLine($"Bundle index: {meta.Index}");
        sb.AppendLine();
        sb.AppendLine("User question:");
        sb.AppendLine(userMessage.Trim());
        sb.AppendLine();
        sb.AppendLine("Authoritative bundle JSON:");
        sb.AppendLine(bundleJson);
        sb.AppendLine();
        sb.AppendLine($"Reply in {responseLocale}. Use only this JSON. No greeting. No markdown tables unless user asked.");
        sb.AppendLine("If insufficient, one sentence stating what is missing.");
        sb.AppendLine();
        sb.AppendLine("AI:");
        return sb.ToString();
    }

    private static string BuildPlainPrompt(string userMessage)
    {
        var sb = new StringBuilder();
        sb.Append("[Server clock: ")
            .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            .AppendLine(" UTC]");
        sb.Append("User: ").AppendLine(userMessage.Trim());
        sb.Append("AI:");
        return sb.ToString();
    }
}
