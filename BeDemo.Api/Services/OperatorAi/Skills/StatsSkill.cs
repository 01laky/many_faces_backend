using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Platform-statistics skill — a thin wrapper over the shipped RAG v1 flow (D8): retrieve stat bundles →
/// fresh-load → per-bundle map → stitch. Behaviour for stats questions is unchanged from RAG v1; this just
/// composes the existing retriever + orchestrator behind the skill interface. Trusted (aggregate counts only).
///
/// 7B-perf: the terminal operator-visible generation (single-bundle answer or multi-bundle synthesis) is produced
/// via the orchestrator's <see cref="IOperatorAiLiveStatsOrchestrator.PrepareSelectedAsync"/> terminal plan, so the
/// skill can either return it whole (count fast-path — 0 generations) or STREAM it token by token (O4) while the
/// per-bundle maps stay internal. The per-turn trace carries the O9 fields (fast-path, generation count, latencies).
/// </summary>
public sealed class StatsSkill : IOperatorAiStreamingSkill
{
	/// <summary>Fixed English refusal when retrieval finds nothing usable (§6.1) — lifted from the RAG-v1 ChatHub branch.</summary>
	internal const string ZeroHitRefusal =
		"Many Faces AI only answers questions about data within the application. "
		+ "I couldn't find anything in the platform data that matches your question.";

	private readonly IOperatorAiRetriever _retriever;
	private readonly IOperatorAiLiveStatsOrchestrator _orchestrator;
	private readonly IAiGrpcService _ai;
	private readonly IOperatorAiDecisionHelper _decisions;

	public StatsSkill(
		IOperatorAiRetriever retriever,
		IOperatorAiLiveStatsOrchestrator orchestrator,
		IAiGrpcService ai,
		IOperatorAiDecisionHelper decisions)
	{
		_retriever = retriever;
		_orchestrator = orchestrator;
		_ai = ai;
		_decisions = decisions;
	}

	public string Id => "stats";
	public string DisplayName => "Platform statistics";

	public string Description =>
		"Answer questions about platform counts, totals, trends and breakdowns — users, albums, blogs, reels, "
		+ "stories, friends, messages, chat rooms, pages, faces, the moderation queue and more.";

	public IReadOnlyList<string> SampleRequests =>
		[
			"how many users signed up this week?",
			"how many albums are pending approval?",
			"chat message volume trend",
			"how many reels failed AI review?",
			"total number of faces and pages",
		];

	public string RouterHint =>
		"platform counts, totals, trends and breakdowns of any entity (users, albums, reels, stories, faces, "
		+ "messages, the moderation-queue size, …) — the answer is a number";

	public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;

	public async Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		// SELECTION (§6/§17): RAG-first; planner fallback + zero-hit escalation handled inside the retriever.
		var retrieval = await _retriever.RetrieveBundleIndicesAsync(request.UserMessage, cancellationToken);

		// Broad = "map ALL 61 bundles" (full-platform overview). The helper upgrades a keyword-miss to broad when the
		// question is metrics-like and not a simple count; deterministic keywords remain the fallback (O19, §3.2).
		var isBroad = await _decisions.IsBroadOverviewAsync(request.UserMessage, cancellationToken);
		IReadOnlyList<int> indices;
		if (isBroad)
		{
			// Full-stats broad-overview (§3.2): map ALL 61 bundles — RAG-ranked first, then the rest in catalog
			// order — so the deterministic stitch covers every entity. No zero-hit refusal here (all 61 exist).
			var ranked = retrieval.BundleIndices;
			indices = ranked
				.Concat(Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount).Where(i => !ranked.Contains(i)))
				.ToList();
		}
		else
		{
			if (retrieval.IsZeroHit || retrieval.BundleIndices.Count == 0)
				return new OperatorAiSkillResult(ZeroHitRefusal, Trace: ZeroHitTrace(sw));
			indices = retrieval.BundleIndices;
		}

		// Build the terminal plan (load + fast-paths + maps + stitch), then run the terminal generation inline.
		var plan = await _orchestrator.PrepareSelectedAsync(
			request.UserMessage,
			indices,
			request.MaxParallelBundleAiCalls ?? 1,
			appendCoverageNote: isBroad,
			broadOverview: isBroad,
			cancellationToken);

		string answer;
		if (plan.IsComplete)
		{
			answer = plan.CompleteAnswer!;
		}
		else
		{
			var generated = await _ai.GenerateAsync(
				plan.StreamPrompt!,
				plan.StreamMaxNewTokens,
				responseLocale: "en",
				cancellationToken: cancellationToken);
			answer = string.IsNullOrWhiteSpace(generated) || IsErrorText(generated) ? plan.FallbackText : generated.Trim();
		}

		return new OperatorAiSkillResult(answer, Trace: PlanTrace(sw, plan, retrieval.FallbackUsed, terminalGenerated: !plan.IsComplete));
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<OperatorAiStreamChunk> RunStreamingAsync(
		OperatorAiSkillRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		var retrieval = await _retriever.RetrieveBundleIndicesAsync(request.UserMessage, cancellationToken);

		var isBroad = await _decisions.IsBroadOverviewAsync(request.UserMessage, cancellationToken);
		IReadOnlyList<int> indices;
		if (isBroad)
		{
			// Full-stats broad-overview (§3.2): map ALL 61 bundles — RAG-ranked first, then the rest in catalog
			// order. With the deterministic stitch this plan is IsComplete, so it streams as one block below.
			var ranked = retrieval.BundleIndices;
			indices = ranked
				.Concat(Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount).Where(i => !ranked.Contains(i)))
				.ToList();
		}
		else if (retrieval.IsZeroHit || retrieval.BundleIndices.Count == 0)
		{
			yield return new OperatorAiStreamChunk(ZeroHitRefusal, IsFinal: true, FinalAnswer: ZeroHitRefusal, Trace: ZeroHitTrace(sw));
			yield break;
		}
		else
		{
			indices = retrieval.BundleIndices;
		}

		var plan = await _orchestrator.PrepareSelectedAsync(
			request.UserMessage,
			indices,
			request.MaxParallelBundleAiCalls ?? 1,
			appendCoverageNote: isBroad,
			broadOverview: isBroad,
			cancellationToken);

		// Count fast-path / synthesis-off: nothing to stream — emit the ready answer as one chunk.
		if (plan.IsComplete)
		{
			var complete = plan.CompleteAnswer!;
			// operator-ai degraded-handling D3 — when the plan is the all-failed sentinel ("Error: …"), do NOT stream it
			// as a visible delta; emit only the final, which the hub's ShouldNotPersist turns into a clean ephemeral
			// "AI unavailable" message (no raw error text flashes in the UI, nothing persisted).
			if (!IsErrorText(complete))
				yield return new OperatorAiStreamChunk(complete, IsFinal: false);
			yield return new OperatorAiStreamChunk(null, IsFinal: true, FinalAnswer: complete, Trace: PlanTrace(sw, plan, retrieval.FallbackUsed, terminalGenerated: false));
			yield break;
		}

		// Stream the terminal generation (single-bundle answer or synthesis), accumulating the full text to persist.
		var acc = new StringBuilder();
		var sawError = false;
		await foreach (var delta in _ai.GenerateStreamAsync(
			plan.StreamPrompt!,
			plan.StreamMaxNewTokens,
			responseLocale: "en",
			cancellationToken: cancellationToken).WithCancellation(cancellationToken))
		{
			if (delta.HasError)
			{
				sawError = true;
				break;
			}
			if (!string.IsNullOrEmpty(delta.TextDelta))
			{
				acc.Append(delta.TextDelta);
				yield return new OperatorAiStreamChunk(delta.TextDelta, IsFinal: false);
			}
			if (delta.IsFinal)
				break;
		}

		var streamed = acc.ToString().Trim();
		var final = sawError || streamed.Length == 0 ? plan.FallbackText : streamed;
		yield return new OperatorAiStreamChunk(null, IsFinal: true, FinalAnswer: final, Trace: PlanTrace(sw, plan, retrieval.FallbackUsed, terminalGenerated: true));
	}

	private OperatorAiSkillTrace ZeroHitTrace(Stopwatch sw)
	{
		sw.Stop();
		return new OperatorAiSkillTrace(Id, UsedRetrieval: true, FellBackInternally: true, sw.ElapsedMilliseconds, FastPath: "zero-hit");
	}

	private OperatorAiSkillTrace PlanTrace(Stopwatch sw, OperatorAiTerminalPlan plan, bool fellBack, bool terminalGenerated)
	{
		sw.Stop();
		var generations = plan.Trace.Generations + (terminalGenerated ? 1 : 0);
		return new OperatorAiSkillTrace(
			Id,
			UsedRetrieval: true,
			FellBackInternally: fellBack,
			sw.ElapsedMilliseconds,
			FastPath: plan.Trace.FastPath,
			Generations: generations,
			LoadMs: plan.Trace.LoadMs,
			MapMs: plan.Trace.MapMs,
			Degraded: plan.Trace.Degraded);
	}

	/// <summary>The gRPC service returns an "Error: ..." string for transport failures; treat that as a failed generation. Shared with the per-bundle map path (single source of truth).</summary>
	private static bool IsErrorText(string text) => OperatorAiGenerationErrors.IsErrorText(text);
}
