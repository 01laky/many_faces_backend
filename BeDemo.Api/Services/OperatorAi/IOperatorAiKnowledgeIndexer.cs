namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Outcome of a knowledge-index rebuild (returned to the admin reindex endpoint, §7.2/§8.1).</summary>
/// <param name="IndexedCount">Documents successfully upserted.</param>
/// <param name="FailedCount">Documents the worker rejected.</param>
/// <param name="EmbedModelVersion">Embed model version reported by the worker (drives re-embed on bump).</param>
/// <param name="Skipped">True when the content-hash marker matched and no work was done (idempotent skip).</param>
/// <param name="Coalesced">True when this call coalesced behind an in-flight rebuild (single-flight, §17.5).</param>
/// <param name="Error">Set when the rebuild could not run (embed/worker unavailable, etc.).</param>
public sealed record OperatorAiKnowledgeReindexResult(
	int IndexedCount,
	int FailedCount,
	string? EmbedModelVersion,
	bool Skipped,
	bool Coalesced,
	string? Error);

/// <summary>
/// Builds the 61 stat-bundle <c>KnowledgeDocument</c>s (descriptor + synonyms + sample questions), embeds each
/// <c>content_text</c> via <see cref="IAiGrpcService.EmbedTextAsync"/>, and bulk-upserts them through the
/// search-worker (§7/§8). Idempotent via a content-hash marker; guarded by a single-flight lock (§17.5).
/// </summary>
public interface IOperatorAiKnowledgeIndexer
{
	/// <summary>
	/// Rebuild the knowledge index. When <paramref name="force"/> is false and the content-hash marker matches
	/// the current <c>{catalogVersion, descriptor texts, embed_model_version}</c>, the rebuild is skipped
	/// (startup path). When true, all 61 docs are re-embedded and upserted (admin path).
	/// </summary>
	Task<OperatorAiKnowledgeReindexResult> RebuildAsync(bool force, CancellationToken cancellationToken = default);

	/// <summary>True when a rebuild is currently in flight (drives the admin 409, §17.5).</summary>
	bool IsRebuildInProgress { get; }
}
