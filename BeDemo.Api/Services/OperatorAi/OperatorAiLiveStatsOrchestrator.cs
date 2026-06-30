using System.Diagnostics;
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
	/// <param name="appendCoverageNote">True for broad-overview answers — appends a coverage note (§6).</param>
	/// <param name="broadOverview">Full-stats fix: when true, all 61 bundles are mapped per-bundle on the CPU
	/// helper at <see cref="OperatorAiOptions.BroadOverviewMaxParallel"/> and the answer is the DETERMINISTIC
	/// stitch (no AI synthesis), so nothing can be truncated.</param>
	Task<string> RunWithSelectedIndicesAsync(
		string userMessage,
		IReadOnlyList<int> indices,
		int maxParallelBundleAiCalls,
		bool appendCoverageNote,
		bool broadOverview = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 7B-perf O2/O3/O4 — do everything for a selected-index turn EXCEPT the final operator-visible generation, and
	/// return a <see cref="OperatorAiTerminalPlan"/>. The plan is either a ready <c>CompleteAnswer</c> (count fast-path
	/// — 0 generations — or a stitched draft when synthesis is off) or a <c>StreamPrompt</c> to be generated (the
	/// single-bundle answer or the multi-bundle synthesis). Callers run the terminal generation non-streamed
	/// (<see cref="IAiGrpcService.GenerateAsync"/>) or streamed (<see cref="IAiGrpcService.GenerateStreamAsync"/>),
	/// which is what lets the operator-visible answer stream while the per-bundle maps stayed internal.
	/// </summary>
	Task<OperatorAiTerminalPlan> PrepareSelectedAsync(
		string userMessage,
		IReadOnlyList<int> indices,
		int maxParallelBundleAiCalls,
		bool appendCoverageNote,
		bool broadOverview = false,
		CancellationToken cancellationToken = default);
}

/// <summary>7B-perf O9 — per-turn stage trace for the selected-index path (latencies + generation count + which fast-path fired).</summary>
public sealed record OperatorAiLiveTurnTrace(
	string FastPath,
	int Generations,
	long LoadMs,
	long MapMs,
	int BundleCount,
	// operator-ai degraded-handling D10 — PII-free degradation signal: the turn ran but ≥1 bundle section could not
	// be produced (model down/timed out). Surfaced to the admin AI overview / logs without re-deriving it.
	bool Degraded = false,
	int FailedBundles = 0);

/// <summary>
/// 7B-perf O2/O3/O4 — the result of <see cref="IOperatorAiLiveStatsOrchestrator.PrepareSelectedAsync"/>. Exactly one
/// of <see cref="CompleteAnswer"/> / <see cref="StreamPrompt"/> is non-null: a complete answer needs no further
/// generation (count fast-path, or synthesis-off draft); a stream prompt is the terminal operator-visible generation.
/// </summary>
public sealed record OperatorAiTerminalPlan(
	string? CompleteAnswer,
	string? StreamPrompt,
	int StreamMaxNewTokens,
	OperatorAiLiveTurnTrace Trace,
	string? FallbackAnswer = null)
{
	/// <summary>True when there is nothing left to generate (the answer is ready).</summary>
	public bool IsComplete => CompleteAnswer is not null;

	/// <summary>Graceful text to use when the terminal generation fails/times out (the stitched facts for synthesis).</summary>
	public string FallbackText =>
		FallbackAnswer is { Length: > 0 } f ? f : "No statistics data was available to answer this question.";
}

/// <inheritdoc />
public sealed class OperatorAiLiveStatsOrchestrator : IOperatorAiLiveStatsOrchestrator
{
	/// <summary>
	/// operator-ai degraded-handling D3 — sentinel returned as the terminal plan's answer when EVERY bundle section
	/// failed (model down mid-turn). It is an `"Error: …"` string, which <c>OperatorAiResponseGuard.ShouldNotPersist</c>
	/// recognises as an infrastructure failure, so the hub turns it into a single honest "AI unavailable" ephemeral and
	/// does not persist it. Kept here (not localized) on purpose: the user-facing copy comes from the localized hub
	/// error code, this string only carries the failure classification.
	/// </summary>
	internal const string AllBundlesFailedSentinel = "Error: AI service unavailable (all bundle sections failed)";

	/// <summary>
	/// operator-ai degraded-handling D15 — sentinel returned by a skill when the readiness re-check immediately before
	/// the terminal/synthesis generation finds the model is still LOADING (not yet ready, but reachable). It carries the
	/// <c>MODEL_LOAD</c> marker that <c>OperatorAiResponseGuard.IsTransientStatusMessage</c> recognises, so the hub maps
	/// it to the localized <c>ModelLoading</c> ephemeral (distinct from the <see cref="AllBundlesFailedSentinel"/>
	/// "unavailable" case) and does not persist it. Like the all-failed sentinel this is a classification token, not
	/// user copy — the operator-facing text comes from the resx-localized hub error code.
	/// </summary>
	internal const string ModelLoadingSentinel = "MODEL_LOADING (model warming up, not yet ready)";

	private readonly IOperatorAiLiveStatsPrefetcher _prefetcher;
	private readonly IAiGrpcService _ai;
	private readonly IOperatorAiDecisionHelper _decisionHelper;
	private readonly OperatorAiOptions _options;
	private readonly AiServiceOptions _aiOptions;
	private readonly ILogger<OperatorAiLiveStatsOrchestrator> _logger;

	public OperatorAiLiveStatsOrchestrator(
		IOperatorAiLiveStatsPrefetcher prefetcher,
		IAiGrpcService ai,
		IOperatorAiDecisionHelper decisionHelper,
		IOptions<OperatorAiOptions> options,
		IOptions<AiServiceOptions> aiOptions,
		ILogger<OperatorAiLiveStatsOrchestrator> logger)
	{
		_prefetcher = prefetcher;
		_ai = ai;
		_decisionHelper = decisionHelper;
		_options = options.Value;
		_aiOptions = aiOptions.Value;
		_logger = logger;
	}

	/// <summary>
	/// 7B-perf O19 Role B (default off) — the per-bundle model for map position <paramref name="position"/>. When the
	/// experimental heterogeneous parallel map is enabled AND a CPU-resident helper model is configured, odd-positioned
	/// bundles run on the helper (CPU) while even ones stay on the GPU 7B, so the two devices map in parallel. Off ⇒
	/// null (worker default model). Validate with the O9 benchmark before enabling — it trades quality for throughput.
	/// </summary>
	private string? MapModelFor(int position) =>
		_options.HelperParallelMapEnabled && !string.IsNullOrWhiteSpace(_aiOptions.HelperModel) && (position % 2 == 1)
			? _aiOptions.HelperModel
			: null;

	/// <summary>
	/// Full-stats broad-overview map model: ALL 61 per-bundle maps run on the CPU helper (when configured) so the
	/// GPU stays free and the maps parallelise on the CPU. Falls back to the worker default model (7B) when no
	/// helper is set (correct, just slower and GPU-serial).
	/// </summary>
	private string? BroadMapModel() =>
		!string.IsNullOrWhiteSpace(_aiOptions.HelperModel) ? _aiOptions.HelperModel : null;

	/// <summary>Map-step sampling override (7B-perf O11): low temperature + stop sequences for the terse facts step.</summary>
	private IReadOnlyList<string>? MapStops =>
		_options.MapStopSequences is { Length: > 0 } ? _options.MapStopSequences : null;

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
		var partTasks = planner.Indices.Select((index, position) => RunBundleAiAsync(
			index,
			MapModelFor(position),
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
		bool broadOverview = false,
		CancellationToken cancellationToken = default)
	{
		// Non-streaming entry point (back-compat): build the terminal plan, then run the terminal generation inline.
		var plan = await PrepareSelectedAsync(userMessage, indices, maxParallelBundleAiCalls, appendCoverageNote, broadOverview, cancellationToken);
		if (plan.IsComplete)
			return plan.CompleteAnswer!;

		using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		budgetCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1_000, _options.OverallTurnBudgetMs)));
		try
		{
			var terminal = await _ai.GenerateAsync(
				plan.StreamPrompt!,
				plan.StreamMaxNewTokens,
				responseLocale: "en",
				cancellationToken: budgetCts.Token);
			return string.IsNullOrWhiteSpace(terminal) ? plan.FallbackText : terminal.Trim();
		}
		catch (OperationCanceledException)
		{
			return plan.FallbackText;
		}
	}

	/// <inheritdoc />
	public async Task<OperatorAiTerminalPlan> PrepareSelectedAsync(
		string userMessage,
		IReadOnlyList<int> indices,
		int maxParallelBundleAiCalls,
		bool appendCoverageNote,
		bool broadOverview = false,
		CancellationToken cancellationToken = default)
	{
		// Always-English (D10): the AI is never sent a language; the locale arg downstream is fixed "en".
		const string responseLocale = "en";

		if (indices.Count == 0)
		{
			// Caller (skill) decides the zero-hit refusal; this method only maps + stitches.
			return new OperatorAiTerminalPlan(string.Empty, null, 0, new OperatorAiLiveTurnTrace("empty", 0, 0, 0, 0));
		}

		// Overall turn budget (§17.7): the whole map+stitch must finish within OverallTurnBudgetMs. On overrun the
		// linked token cancels in-flight Generates; we still stitch whatever sub-answers completed in time.
		using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		budgetCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1_000, _options.OverallTurnBudgetMs)));

		// Full-stats broad-overview (§3.3): the broad path maps all 61 bundles per-bundle on the CPU helper, so it
		// uses its own parallelism (BroadOverviewMaxParallel) and is NOT pinned to MaxParallelBundleAiCalls = 1.
		var parallel = broadOverview
			? Math.Max(1, _options.BroadOverviewMaxParallel)
			: Math.Clamp(maxParallelBundleAiCalls, 1, _options.MaxParallelBundleAiCalls);

		// Stage 1 — fresh-load ONLY the K selected bundles (drop the always-all-61 prefetch, §3.3 / RT-6).
		var loadSw = Stopwatch.StartNew();
		var prefetch = await _prefetcher.PrefetchSelectedAsync(indices, budgetCts.Token);
		var cache = prefetch.Entries;
		loadSw.Stop();

		// ── 7B-perf O2/O3 — single-bundle fast-paths ──────────────────────────────
		// With exactly one bundle there is nothing to stitch across or synthesize from. If it is also a simple count
		// question, answer deterministically (0 generations); otherwise stream a single focused Generate (1 generation).
		if (indices.Count == 1)
		{
			var index = indices[0];
			var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
			cache.TryGetValue(index, out var entry);
			var ready = entry is { State: OperatorAiBundleCacheState.Ready, JsonPayload: { Length: > 0 } };

			// O2 — deterministic count fast-path (0 generations).
			if (ready && await _decisionHelper.IsSimpleCountAsync(userMessage, budgetCts.Token))
			{
				var formatted = OperatorAiCountFastPath.TryFormat(meta, entry!.JsonPayload);
				if (formatted is not null)
				{
					_logger.LogInformation("Operator AI count fast-path (index {Index}, 0 generations).", index);
					return new OperatorAiTerminalPlan(
						formatted,
						null,
						0,
						new OperatorAiLiveTurnTrace("count", 0, loadSw.ElapsedMilliseconds, 0, 1));
				}
			}

			// O3 — single-bundle fast-path: stream one focused Generate, no stitch/synthesis.
			if (ready)
			{
				var prompt = BuildBundlePrompt(userMessage, meta, entry!.JsonPayload!);
				return new OperatorAiTerminalPlan(
					null,
					prompt,
					_options.LiveStitchMaxNewTokens,
					new OperatorAiLiveTurnTrace("single-bundle", 0, loadSw.ElapsedMilliseconds, 0, 1));
			}
			// Not ready ⇒ fall through to the generic path which emits the deterministic "data unavailable" stitch.
		}

		// ── Multi-bundle path: per-bundle map → stitch → (optional) synthesis ─────
		var mapSw = Stopwatch.StartNew();
		OperatorAiLiveStatsStitch.Part[] parts;
		int generations;

		if (broadOverview && _options.LiveBroadDeterministicCounts)
		{
			// Full-stats broad-overview on real hardware: a whole-platform snapshot is a pure counts read-out, so we
			// render each of the 61 bundles DETERMINISTICALLY from its already-loaded JSON (OperatorAiCountFastPath)
			// with ZERO Generate calls. The previous design ran one per-bundle LLM map each — 61 maps on the CPU
			// helper never fit OverallTurnBudgetMs (they all hit the per-bundle timeout and dropped → empty answer).
			// A bundle whose JSON has no usable totalCount is the only "unavailable" section (feeds the coverage note).
			parts = indices.Select(index =>
			{
				var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
				cache.TryGetValue(index, out var entry);
				var ready = entry is { State: OperatorAiBundleCacheState.Ready, JsonPayload: { Length: > 0 } };
				var formatted = ready ? OperatorAiCountFastPath.TryFormat(meta, entry!.JsonPayload) : null;
				return formatted is not null
					? new OperatorAiLiveStatsStitch.Part(index, meta.Id, formatted, Failed: false)
					: new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true);
			}).ToArray();
			generations = 0;
			_logger.LogInformation("Operator AI broad-overview deterministic snapshot: {Ready}/{Total} bundles rendered, 0 generations.",
				parts.Count(p => !p.Failed), parts.Length);
		}
		else
		{
			using var gate = new SemaphoreSlim(parallel, parallel);
			// operator-ai degraded-handling D14 — early-abort source, linked to the overall turn budget. The FIRST bundle
			// that hits a hard model "Error:" (the worker/model is down, confirmed after the bounded D13 retry) cancels
			// this source; the remaining queued maps then stop at their semaphore wait / mid-Generate instead of every
			// one of K hammering the same down model, and the turn goes straight to the honest all-failed message.
			using var hardErrorCts = CancellationTokenSource.CreateLinkedTokenSource(budgetCts.Token);
			var partTasks = indices.Select((index, position) => RunBundleAiWithTimeoutAsync(
				index,
				broadOverview ? BroadMapModel() : MapModelFor(position),
				userMessage,
				responseLocale,
				cache,
				gate,
				hardErrorCts.Token,
				onHardModelError: hardErrorCts.Cancel)).ToArray();

			try
			{
				parts = await Task.WhenAll(partTasks);
			}
			catch (OperationCanceledException)
			{
				// Overall budget exceeded OR a D14 early-abort fired mid-flight: salvage the sub-answers that already
				// completed (§17.7). Task.WhenAll only completes once every task is terminal, so reading them here is safe.
				parts = partTasks
					.Where(t => t.IsCompletedSuccessfully)
					.Select(t => t.Result)
					.ToArray();
			}
			generations = parts.Count(p => !p.Failed);
		}
		mapSw.Stop();

		// operator-ai degraded-handling D3/D10 — how many sections could not be produced this turn.
		var failedBundles = parts.Count(p => p.Failed);
		var producedBundles = parts.Length - failedBundles;
		var degraded = failedBundles > 0;
		var deterministicBroad = broadOverview && _options.LiveBroadDeterministicCounts;

		// operator-ai degraded-handling D19 — record per-bundle map outcomes (produced vs failed) for the per-bundle
		// failure rate, but only on the AI-generated map path: the deterministic broad read-out's "failures" are
		// data-not-ready bundles, not AI failures, so they would distort the AI failure-rate signal.
		if (!deterministicBroad)
			OperatorAiMetrics.RecordBundleOutcomes(producedBundles, failedBundles);

		// D3 — on the AI-generated path (not the deterministic broad counts read-out, which is sourced from already-loaded
		// DB JSON), if EVERY section failed the model is effectively down. Return an infrastructure sentinel that the
		// hub's ShouldNotPersist catches → one honest "AI unavailable" ephemeral, NOT a persisted wall of "data
		// unavailable" lines and NOT a model-narrated apology.
		if (!deterministicBroad && parts.Length > 0 && producedBundles == 0 && parts.Any(p => p.AiError))
		{
			_logger.LogWarning(
				"Operator AI stats turn fully degraded: all {Count} bundle section(s) failed, model error present.",
				parts.Length);
			return new OperatorAiTerminalPlan(
				AllBundlesFailedSentinel,
				null,
				0,
				new OperatorAiLiveTurnTrace("all-failed", generations, loadSw.ElapsedMilliseconds, mapSw.ElapsedMilliseconds, indices.Count, Degraded: true, FailedBundles: failedBundles));
		}

		// Optional (config-gated, default off) — all-or-nothing partial policy. When enabled, a partial AI failure
		// (some sections produced, some failed) is treated as a whole-turn outage: return the same honest "AI
		// unavailable" ephemeral instead of partial facts + a coverage note (the default D4 behaviour).
		if (!deterministicBroad && _options.LivePartialFailureAllOrNothing && failedBundles > 0 && producedBundles > 0 && parts.Any(p => p.AiError))
		{
			_logger.LogWarning(
				"Operator AI stats turn partially degraded ({Failed}/{Total} failed); all-or-nothing policy → unavailable.",
				failedBundles,
				parts.Length);
			return new OperatorAiTerminalPlan(
				AllBundlesFailedSentinel,
				null,
				0,
				new OperatorAiLiveTurnTrace("all-failed", generations, loadSw.ElapsedMilliseconds, mapSw.ElapsedMilliseconds, indices.Count, Degraded: true, FailedBundles: failedBundles));
		}

		// Stage 5 — deterministic stitch. The broad deterministic-counts path already produces a fully-labelled line
		// per bundle ("**Users:** 33 total — …"), so it is joined directly: routing it through the stitch would prepend
		// the raw bundle id as a second header ("**entity.users:**" above "**Users:** …"). Failed bundles carry no
		// usable line and are summarised by the coverage note below, so they are simply omitted from the body.
		var draft = deterministicBroad
			? string.Join("\n", parts.Where(p => !p.Failed && !string.IsNullOrWhiteSpace(p.Text)).Select(p => p.Text.Trim()))
			: OperatorAiLiveStatsStitch.Stitch(parts);

		// Coverage note (§3.5): broad-overview covers ALL 61 bundles → a "full snapshot" (partial only when a bundle
		// genuinely failed); the focused path adds a deterministic note when SOME sections could not be loaded (D4) so
		// the model never has to narrate *why* data is missing.
		if (broadOverview)
		{
			draft += failedBundles == 0
				? "\n\n_(Full platform snapshot — all data areas.)_"
				: $"\n\n_(Full platform snapshot — {failedBundles} data area(s) were temporarily unavailable.)_";
		}
		else if (failedBundles > 0)
		{
			draft += $"\n\n_(Note: {failedBundles} of {parts.Length} data area(s) could not be loaded right now.)_";
		}
		else if (appendCoverageNote && indices.Count > 0)
		{
			draft += $"\n\n_(This answer covers the {indices.Count} most relevant data areas for your question.)_";
		}

		// Full-stats broad-overview (§3.3): force the DETERMINISTIC stitch (skip AI synthesis) — the concatenation of
		// all 61 per-bundle map outputs is complete by construction, so there is no final Generate to truncate it.
		var useSynthesis = _options.LiveUseAiSynthesisStitch && !broadOverview;

		// When synthesis is off (or there is nothing to synthesize), the stitched draft IS the answer (0 extra gen).
		if (!useSynthesis || string.IsNullOrWhiteSpace(draft))
		{
			return new OperatorAiTerminalPlan(
				draft,
				null,
				0,
				new OperatorAiLiveTurnTrace("stitch", generations, loadSw.ElapsedMilliseconds, mapSw.ElapsedMilliseconds, indices.Count, Degraded: degraded, FailedBundles: failedBundles));
		}

		// Otherwise the operator-visible answer is the synthesis Generate — return it as a stream prompt so the caller
		// can stream it (O4) or generate it inline. The stitched draft is the graceful fallback if synthesis fails.
		var synthesisPrompt = OperatorAiLiveStatsPlanner.BuildSynthesisPrompt(userMessage, draft, responseLocale);
		return new OperatorAiTerminalPlan(
			null,
			synthesisPrompt,
			_options.LiveStitchMaxNewTokens,
			new OperatorAiLiveTurnTrace("synthesis", generations, loadSw.ElapsedMilliseconds, mapSw.ElapsedMilliseconds, indices.Count, Degraded: degraded, FailedBundles: failedBundles),
			FallbackAnswer: draft);
	}

	/// <summary>
	/// Wrap <see cref="RunBundleAiAsync"/> with the per-bundle Generate timeout (§17.7). On timeout (but not an
	/// overall-budget cancel) we return a Failed part so the stitch emits a one-line "data unavailable" note for
	/// just that section instead of failing the whole turn.
	/// </summary>
	private async Task<OperatorAiLiveStatsStitch.Part> RunBundleAiWithTimeoutAsync(
		int index,
		string? model,
		string userMessage,
		string responseLocale,
		IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> cache,
		SemaphoreSlim gate,
		CancellationToken turnToken,
		Action? onHardModelError = null)
	{
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
		using var perBundleCts = CancellationTokenSource.CreateLinkedTokenSource(turnToken);
		perBundleCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1_000, _options.PerBundleGenerateTimeoutMs)));

		try
		{
			return await RunBundleAiAsync(index, model, userMessage, responseLocale, cache, gate, perBundleCts.Token, onHardModelError);
		}
		catch (OperationCanceledException) when (!turnToken.IsCancellationRequested)
		{
			// Per-bundle timeout only — the turn budget is still alive; degrade this one section.
			_logger.LogWarning("Per-bundle Generate timed out for index {Index} ({Id}); dropping section.", index, meta.Id);
			return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true, AiError: true);
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
		string? model,
		string userMessage,
		string responseLocale,
		IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> cache,
		SemaphoreSlim gate,
		CancellationToken cancellationToken,
		Action? onHardModelError = null)
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
			var prompt = BuildBundlePrompt(userMessage, meta, entry.JsonPayload);
			// 7B-perf O11 — terse facts: low temperature + stop sequences so the map stops early instead of padding
			// to the token cap or drifting into a new turn (the operator-visible synthesis keeps fluent sampling).
			// operator-ai degraded-handling D13 — one bounded retry: GenerateAsync returns an "Error: …" STRING (not an
			// exception) on a transient transport blip, and a single blip should not drop a whole section. The
			// per-bundle timeout + overall budget still bound total work and we are inside the parallelism gate.
			string? text = null;
			for (var attempt = 0; attempt < 2; attempt++)
			{
				text = await _ai.GenerateAsync(
					prompt,
					_options.LiveBundleMaxNewTokens,
					responseLocale: responseLocale,
					temperature: _options.MapTemperature,
					stopSequences: MapStops,
					model: model,
					cancellationToken: cancellationToken);
				if (!OperatorAiGenerationErrors.IsUnusable(text))
					break;
			}

			// operator-ai degraded-handling D1 — an "Error: …" string (or empty) is a FAILURE, not facts. Mark the
			// section Failed so the stitch renders a clean "data unavailable" note and the error text NEVER reaches the
			// synthesis prompt (where the model used to narrate it into the user-facing answer).
			if (OperatorAiGenerationErrors.IsErrorText(text))
			{
				// D14 — a HARD model error ("Error: …" that survived the bounded D13 retry) means the worker/model is
				// down, not a one-off empty section. Signal the map to early-abort so the remaining queued bundle
				// Generates do not fire; the all-failed detection in PrepareSelectedAsync then surfaces one honest
				// "AI unavailable" message instead of mapping every bundle only to discover the same down model.
				onHardModelError?.Invoke();
				return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true, AiError: true);
			}

			return string.IsNullOrWhiteSpace(text)
				? new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true, AiError: true)
				: new OperatorAiLiveStatsStitch.Part(index, meta.Id, text!, Failed: false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Bundle AI failed for index {Index}", index);
			return new OperatorAiLiveStatsStitch.Part(index, meta.Id, string.Empty, Failed: true, AiError: true);
		}
		finally
		{
			gate.Release();
		}
	}

	// 7B-perf O12 — trimmed map prompt. Prefill time scales with prompt length on a partial-GPU 7B, so this keeps the
	// instruction minimal (id + question + JSON + one terse rule) instead of the older multi-line boilerplate. The
	// shorter prompt lowers time-to-first-token and KV pressure while preserving answer quality (exact numbers).
	private static string BuildBundlePrompt(
		string userMessage,
		OperatorAiBundleCatalogEntryDto meta,
		string bundleJson)
	{
		var sb = new StringBuilder();
		sb.Append("Bundle ").Append(meta.Id).Append(". Question: ").AppendLine(userMessage.Trim());
		sb.AppendLine("JSON:");
		sb.AppendLine(bundleJson);
		sb.AppendLine("Answer in English with the exact numbers from the JSON, 1-2 sentences, no greeting. Say data is missing only if totalCount is absent or zero.");
		sb.Append("AI:");
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
