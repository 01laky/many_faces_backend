using System.Diagnostics;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Platform-statistics skill — a thin wrapper over the shipped RAG v1 flow (D8): retrieve stat bundles →
/// fresh-load → per-bundle map → stitch. Behaviour for stats questions is unchanged from RAG v1; this just
/// composes the existing retriever + orchestrator behind the skill interface. Trusted (aggregate counts only).
/// </summary>
public sealed class StatsSkill : IOperatorAiSkill
{
	/// <summary>Fixed English refusal when retrieval finds nothing usable (§6.1) — lifted from the RAG-v1 ChatHub branch.</summary>
	internal const string ZeroHitRefusal =
		"Many Faces AI only answers questions about data within the application. "
		+ "I couldn't find anything in the platform data that matches your question.";

	private readonly IOperatorAiRetriever _retriever;
	private readonly IOperatorAiLiveStatsOrchestrator _orchestrator;

	public StatsSkill(IOperatorAiRetriever retriever, IOperatorAiLiveStatsOrchestrator orchestrator)
	{
		_retriever = retriever;
		_orchestrator = orchestrator;
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

	public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;

	public async Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		// SELECTION (§6/§17): RAG-first; planner fallback + zero-hit escalation handled inside the retriever.
		var retrieval = await _retriever.RetrieveBundleIndicesAsync(request.UserMessage, cancellationToken);

		if (retrieval.IsZeroHit || retrieval.BundleIndices.Count == 0)
			return new OperatorAiSkillResult(ZeroHitRefusal, Trace: Trace(sw, usedRetrieval: true, fellBack: true));

		// RETAINED map + stitch over ONLY the fresh-loaded top-K bundles (D6); broad-overview caps + notes (§6).
		var appendCoverageNote = OperatorAiStatsIntent.IsBroadOverviewQuestion(request.UserMessage);
		var answer = await _orchestrator.RunWithSelectedIndicesAsync(
			request.UserMessage,
			retrieval.BundleIndices,
			request.MaxParallelBundleAiCalls ?? 2,
			appendCoverageNote,
			cancellationToken);

		return new OperatorAiSkillResult(answer, Trace: Trace(sw, usedRetrieval: true, fellBack: retrieval.FallbackUsed));
	}

	private OperatorAiSkillTrace Trace(Stopwatch sw, bool usedRetrieval, bool fellBack)
	{
		sw.Stop();
		return new OperatorAiSkillTrace(Id, usedRetrieval, fellBack, sw.ElapsedMilliseconds);
	}
}
