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

	/// <summary>
	/// RAG path (§3.1 query plane, §6): execute the retained map + stitch over a pre-SELECTED set of bundle
	/// indices (chosen upstream by <see cref="IOperatorAiRetriever"/>, not the planner). Fresh-loads ONLY those K
	/// bundles, runs one focused Generate per bundle (max <see cref="OperatorAiOptions.MaxParallelBundleAiCalls"/>
	/// in parallel), then stitches + synthesizes one English reply.
	///
	/// <para>Timeout policy (§17.7):</para>
	/// Per-bundle Generate is bounded by <see cref="OperatorAiOptions.PerBundleGenerateTimeoutMs"/> — a timeout
	/// drops that section with a one-line note rather than failing the turn. The whole turn is bounded by
	/// <see cref="OperatorAiOptions.OverallTurnBudgetMs"/>: when exceeded we stitch whatever sub-answers are ready
	/// and append a coverage note. Never hangs, never empty-errors.
	/// </summary>
	/// <param name="indices">Ordered, deduped, top-K-capped bundle indices from the retriever.</param>
	/// <param name="appendCoverageNote">True for broad-overview answers — appends a "covers the top-K" note (§6).</param>
	Task<string> RunWithSelectedIndicesAsync(
		string userMessage,
		IReadOnlyList<int> indices,
		int maxParallelBundleAiCalls,
		bool appendCoverageNote,
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
		var broadOverview = OperatorAiStatsIntent.IsBroadOverviewQuestion(userMessage);

		// Stage 1 — prefetch all bundles (DB only).
		var prefetchTask = _prefetcher.PrefetchAllAsync(timeoutCts.Token);

		// Stage 2 — planner (skip for broad overview; we use compact all-bundle JSON instead).
		OperatorAiLivePlannerResultDto planner;
		if (broadOverview)
		{
			planner = new OperatorAiLivePlannerResultDto { Indices = [], Reason = "broad-overview" };
		}
		else
		{
			var plannerPrompt = OperatorAiLiveStatsPlanner.BuildPrompt(userMessage, catalog);
			var plannerRaw = await _ai.GenerateAsync(
				plannerPrompt,
				_options.LivePlannerMaxNewTokens,
				responseLocale: "en",
				cancellationToken: timeoutCts.Token);

			planner = OperatorAiLiveStatsPlanner.ParseIndices(
				plannerRaw,
				OperatorAiEntityBundleCatalog.BundleCount,
				_options.MaxSelectedBundleIndices,
				metricsLike);

			var supplemented = OperatorAiLiveStatsPlanner.SupplementIndicesFromMessage(
				userMessage,
				planner.Indices,
				OperatorAiEntityBundleCatalog.BundleCount,
				_options.MaxSelectedBundleIndices);

			planner = new OperatorAiLivePlannerResultDto
			{
				Indices = supplemented,
				Reason = planner.Reason,
			};
		}

		var cache = (await prefetchTask).Entries;

		if (broadOverview)
		{
			var overviewJson = OperatorAiLiveStatsOverview.BuildCompactJson(cache);
			var overviewPrompt = OperatorAiLiveStatsPlanner.BuildOverviewPrompt(
				userMessage,
				overviewJson,
				responseLocale);
			return await _ai.GenerateAsync(
				overviewPrompt,
				_options.LiveStitchMaxNewTokens,
				responseLocale: responseLocale,
				cancellationToken: timeoutCts.Token);
		}

		if (planner.Indices.Count == 0)
		{
			var overviewJson = OperatorAiLiveStatsOverview.BuildCompactJson(cache);
			var fallbackPrompt = OperatorAiLiveStatsPlanner.BuildOverviewPrompt(
				userMessage,
				overviewJson,
				responseLocale);
			return await _ai.GenerateAsync(
				fallbackPrompt,
				_options.LiveStitchMaxNewTokens,
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
		var draft = OperatorAiLiveStatsStitch.Stitch(parts);
		return await SynthesizeAsync(userMessage, responseLocale, draft, timeoutCts.Token);
	}

	/// <inheritdoc />
	public async Task<string> RunWithSelectedIndicesAsync(
		string userMessage,
		IReadOnlyList<int> indices,
		int maxParallelBundleAiCalls,
		bool appendCoverageNote,
		CancellationToken cancellationToken = default)
	{
		// Always-English (D10): the AI is never sent a language; the locale arg downstream is fixed "en".
		const string responseLocale = "en";

		// Overall turn budget (§17.7): the whole map+stitch must finish within OverallTurnBudgetMs. On overrun the
		// linked token cancels in-flight Generates; we still stitch whatever sub-answers completed in time.
		using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		budgetCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1_000, _options.OverallTurnBudgetMs)));

		if (indices.Count == 0)
			return string.Empty; // Caller (ChatHub) decides the zero-hit refusal; this method only maps + stitches.

		var parallel = Math.Clamp(maxParallelBundleAiCalls, 1, _options.MaxParallelBundleAiCalls);

		// Stage 1 — fresh-load ONLY the K selected bundles (drop the always-all-61 prefetch, §3.3 / RT-6).
		var prefetch = await _prefetcher.PrefetchSelectedAsync(indices, budgetCts.Token);
		var cache = prefetch.Entries;

		// Stage 4 — per-bundle map: one focused Generate per bundle, max N concurrent (RETAINED, D6).
		using var gate = new SemaphoreSlim(parallel, parallel);
		var partTasks = indices.Select(index => RunBundleAiWithTimeoutAsync(
			index,
			userMessage,
			responseLocale,
			cache,
			gate,
			budgetCts.Token)).ToArray();

		OperatorAiLiveStatsStitch.Part[] parts;
		try
		{
			parts = await Task.WhenAll(partTasks);
		}
		catch (OperationCanceledException)
		{
			// Overall budget exceeded mid-flight: salvage the sub-answers that already completed (§17.7).
			parts = partTasks
				.Where(t => t.IsCompletedSuccessfully)
				.Select(t => t.Result)
				.ToArray();
		}

		// Stage 5 — deterministic stitch, then optional AI synthesis into one operator-friendly reply.
		var draft = OperatorAiLiveStatsStitch.Stitch(parts);

		// Broad-overview coverage note (§6): tell the operator the answer is limited to the top-K bundles.
		if (appendCoverageNote && indices.Count > 0)
			draft += $"\n\n_(This answer covers the {indices.Count} most relevant data areas for your question.)_";

		// Synthesis is also bounded by the overall budget; on overrun/failure it returns the stitched draft.
		try
		{
			return await SynthesizeAsync(userMessage, responseLocale, draft, budgetCts.Token);
		}
		catch (OperationCanceledException)
		{
			return draft;
		}
	}

	/// <summary>
	/// Wrap <see cref="RunBundleAiAsync"/> with the per-bundle Generate timeout (§17.7). On timeout (but not an
	/// overall-budget cancel) we return a Failed part so the stitch emits a one-line "data unavailable" note for
	/// just that section instead of failing the whole turn.
	/// </summary>
	private async Task<OperatorAiLiveStatsStitch.Part> RunBundleAiWithTimeoutAsync(
		int index,
		string userMessage,
		string responseLocale,
		IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> cache,
		SemaphoreSlim gate,
		CancellationToken turnToken)
	{
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
		using var perBundleCts = CancellationTokenSource.CreateLinkedTokenSource(turnToken);
		perBundleCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1_000, _options.PerBundleGenerateTimeoutMs)));

		try
		{
			return await RunBundleAiAsync(index, userMessage, responseLocale, cache, gate, perBundleCts.Token);
		}
		catch (OperationCanceledException) when (!turnToken.IsCancellationRequested)
		{
			// Per-bundle timeout only — the turn budget is still alive; degrade this one section.
			_logger.LogWarning("Per-bundle Generate timed out for index {Index} ({Id}); dropping section.", index, meta.Id);
			return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);
		}
	}

	private async Task<string> SynthesizeAsync(
		string userMessage,
		string responseLocale,
		string draftAnswer,
		CancellationToken cancellationToken)
	{
		if (!_options.LiveUseAiSynthesisStitch || string.IsNullOrWhiteSpace(draftAnswer))
			return draftAnswer;

		try
		{
			var prompt = OperatorAiLiveStatsPlanner.BuildSynthesisPrompt(
				userMessage,
				draftAnswer,
				responseLocale);
			var synthesized = await _ai.GenerateAsync(
				prompt,
				_options.LiveStitchMaxNewTokens,
				responseLocale: responseLocale,
				cancellationToken: cancellationToken);
			return string.IsNullOrWhiteSpace(synthesized) ? draftAnswer : synthesized.Trim();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Live stats synthesis stitch failed; returning draft answer");
			return draftAnswer;
		}
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
		sb.AppendLine("Authoritative bundle JSON (fields: totalCount, byStatus, byType, byAiReviewStatus, timeseriesLast7Days):");
		sb.AppendLine(bundleJson);
		sb.AppendLine();
		sb.AppendLine($"Reply in {responseLocale}. Answer the user question using totalCount and breakdown fields.");
		sb.AppendLine("State exact numbers from the JSON. 2–4 sentences max. No greeting.");
		sb.AppendLine("Only say data is missing if totalCount is absent or zero and the question needs more detail.");
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
