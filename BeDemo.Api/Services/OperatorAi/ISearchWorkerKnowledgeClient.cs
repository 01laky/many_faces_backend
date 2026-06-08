using ManyFaces.Search.V1;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Thin gRPC client wrapper for the operator-AI RAG knowledge surface on the Go search-worker (§8).
///
/// <para>Pipeline position:</para>
/// This is the backend's only door to the <c>operator-ai-knowledge</c> ES index. The indexer
/// (<see cref="IOperatorAiKnowledgeIndexer"/>) calls <see cref="IndexKnowledgeAsync"/>; the retriever
/// (<see cref="IOperatorAiRetriever"/>) calls <see cref="SemanticSearchAsync"/>; the readiness gate and admin
/// status panel call <see cref="KnowledgeIndexStatusAsync"/>. The "API never talks to ES directly" invariant
/// holds — everything flows backend → search-worker gRPC → Elasticsearch.
///
/// <para>Inputs/outputs:</para>
/// Methods return <c>null</c> when the worker is disabled/unreachable so callers can degrade to the legacy
/// planner (§6) rather than crash. Channel/TLS/auth mirror the existing <c>SearchWorkerGrpcGateway</c>.
/// </summary>
public interface ISearchWorkerKnowledgeClient
{
	/// <summary>True when search is enabled and a gRPC channel exists.</summary>
	bool IsAvailable { get; }

	/// <summary>Bulk upsert KnowledgeDocuments by <c>knowledge_id</c> (index plane). Null ⇒ worker unavailable.</summary>
	Task<IndexKnowledgeResponse?> IndexKnowledgeAsync(IndexKnowledgeRequest request, CancellationToken cancellationToken = default);

	/// <summary>Delete one KnowledgeDocument by id (phase-2 removals). Null ⇒ worker unavailable.</summary>
	Task<DeleteKnowledgeResponse?> DeleteKnowledgeAsync(DeleteKnowledgeRequest request, CancellationToken cancellationToken = default);

	/// <summary>Hybrid kNN + BM25 retrieval (RRF-fused) over descriptors (query plane). Null ⇒ worker unavailable ⇒ planner fallback.</summary>
	Task<SemanticSearchResponse?> SemanticSearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default);

	/// <summary>Readiness + health for the cold-start gate (§17.4) and admin panel (§17.9). Null ⇒ worker unavailable.</summary>
	Task<KnowledgeIndexStatusResponse?> KnowledgeIndexStatusAsync(KnowledgeIndexStatusRequest request, CancellationToken cancellationToken = default);
}
