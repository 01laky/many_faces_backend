namespace BeDemo.Api.Services.OperatorAi;

/// <summary>How the bundle selection was produced (drives the §17.1 trace + metrics outcome label).</summary>
public enum OperatorAiSelectionStrategy
{
	/// <summary>RAG retrieval (EmbedText → SemanticSearch) returned usable hits.</summary>
	Rag,

	/// <summary>Legacy LLM planner fallback (embed/ES down OR index not ready, §6/§17.4).</summary>
	Planner,

	/// <summary>Relaxed retrieval (lower threshold + larger K) — zero-hit escalation attempt 3 (§6.1).</summary>
	Relaxed,

	/// <summary>Nothing usable after all attempts — caller emits the fixed English refusal (§6.1).</summary>
	ZeroHit,
}

/// <summary>One retrieved hit (kept for the trace/debug payloads, §17.1/§17.10).</summary>
public sealed record OperatorAiRetrievalHit(string KnowledgeId, int BundleIndex, double RrfScore, double VectorRank, double TextRank);

/// <summary>
/// Result of the selection step: the ordered bundle indices plus trace metadata. <see cref="BundleIndices"/> is
/// the deterministic, deduped, top-K-capped list fed to the shared map+stitch. Empty + strategy ZeroHit ⇒ refusal.
/// </summary>
public sealed record OperatorAiRetrievalResult(
	IReadOnlyList<int> BundleIndices,
	OperatorAiSelectionStrategy Strategy,
	IReadOnlyList<OperatorAiRetrievalHit> Hits,
	bool Degraded,
	bool EmbedCacheHit,
	long EmbedMs,
	long SearchMs)
{
	public bool IsZeroHit => Strategy == OperatorAiSelectionStrategy.ZeroHit;
	public bool FallbackUsed => Strategy is OperatorAiSelectionStrategy.Planner or OperatorAiSelectionStrategy.Relaxed;
}

/// <summary>
/// Replaces the LLM planner as the bundle SELECTION step (§8). <c>EmbedText(message)</c> →
/// <c>SemanticSearch(top_k=MaxSelectedBundleIndices, source_types=["stat_bundle"])</c> → ordered bundle indices,
/// with a query-embedding cache (§17.8), a cold-start readiness gate → planner fallback (§17.4), and zero-hit
/// escalation (§6.1). The shared map + stitch is unchanged downstream.
/// </summary>
public interface IOperatorAiRetriever
{
	Task<OperatorAiRetrievalResult> RetrieveBundleIndicesAsync(string userMessage, CancellationToken cancellationToken = default);
}
