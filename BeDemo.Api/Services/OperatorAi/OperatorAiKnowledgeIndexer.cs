using System.Security.Cryptography;
using System.Text;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Index-plane orchestrator (§7/§8). Funnels both triggers — the startup hosted service (force:false) and the
/// admin reindex endpoint (force:true) — through one <see cref="RebuildAsync"/>.
///
/// <para>Pipeline:</para>
/// catalog 61 descriptors → build content_text (description + synonyms + sample_questions) → EmbedText →
/// KnowledgeDocument{vector, vector_dim, embed_model_version} → bulk IndexKnowledge (upsert by knowledge_id).
///
/// <para>Idempotency (§7.2):</para>
/// A content hash over {catalogVersion, all descriptor texts, embed_model_version} is stored as a Redis marker.
/// On force:false we skip when the marker already matches (no embed/index calls). force:true always rebuilds.
///
/// <para>Single-flight (§17.5):</para>
/// A process <see cref="SemaphoreSlim"/> coalesces concurrent callers; a distributed Redis <c>SET NX</c> lock
/// (<c>bedemo:operator-ai:reindex-lock</c>) guards multi-instance overlap. The admin endpoint maps a coalesced
/// call to HTTP 409. Reads keep serving from the live index during a rebuild (upsert-by-id is safe).
/// </summary>
public sealed class OperatorAiKnowledgeIndexer : IOperatorAiKnowledgeIndexer
{
	internal const string ReindexLockKey = "bedemo:operator-ai:reindex-lock";
	internal const string ContentHashMarkerKey = "bedemo:operator-ai:knowledge-content-hash";
	private const string StatBundleSourceType = "stat_bundle";

	// Content-hash marker is effectively permanent (refreshed every successful rebuild); 365d TTL is a GC safety net.
	private const long MarkerTtlMs = 365L * 24 * 60 * 60 * 1000;

	// Process-wide single-flight: only one rebuild runs at a time in this instance.
	private readonly SemaphoreSlim _gate = new(1, 1);

	private readonly IAiGrpcService _ai;
	private readonly ISearchWorkerKnowledgeClient _knowledge;
	private readonly IOperatorAiRedisStringStore? _redis;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _operatorOptions;
	private readonly ILogger<OperatorAiKnowledgeIndexer> _logger;

	private volatile bool _inProgress;

	public OperatorAiKnowledgeIndexer(
		IAiGrpcService ai,
		ISearchWorkerKnowledgeClient knowledge,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> operatorOptions,
		ILogger<OperatorAiKnowledgeIndexer> logger,
		IOperatorAiRedisStringStore? redis = null)
	{
		_ai = ai;
		_knowledge = knowledge;
		_redis = redis;
		_aiOptions = aiOptions.Value;
		_operatorOptions = operatorOptions.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public bool IsRebuildInProgress => _inProgress;

	/// <inheritdoc />
	public async Task<OperatorAiKnowledgeReindexResult> RebuildAsync(bool force, CancellationToken cancellationToken = default)
	{
		if (!_knowledge.IsAvailable)
		{
			_logger.LogInformation("Knowledge reindex skipped: search worker disabled/unavailable.");
			return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: true, Coalesced: false, "search_worker_unavailable");
		}

		// Single-flight: a busy semaphore means another rebuild is already running in this process.
		if (!await _gate.WaitAsync(0, cancellationToken))
		{
			_logger.LogInformation("Knowledge reindex coalesced: a rebuild is already in progress.");
			return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: false, Coalesced: true, "reindex_already_running");
		}

		var lockToken = Guid.NewGuid().ToString("N");
		var distributedLockHeld = false;
		_inProgress = true;
		try
		{
			// Distributed lock for multi-instance deployments. Absence of Redis (single-node dev/test) falls
			// back to the process semaphore alone, which is sufficient there.
			if (_redis is not null)
			{
				distributedLockHeld = await _redis.SetNotExistsAsync(
					ReindexLockKey,
					lockToken,
					Math.Max(30, _operatorOptions.LiveBundleCacheLockSeconds),
					cancellationToken);

				if (!distributedLockHeld)
				{
					_logger.LogInformation("Knowledge reindex coalesced via distributed lock (another instance is rebuilding).");
					return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: false, Coalesced: true, "reindex_already_running");
				}
			}

			var contentHash = ComputeContentHash();

			// Idempotency: skip the embed + index work when the marker already matches (force:false only).
			if (!force && _redis is not null)
			{
				var existing = await _redis.GetAsync(ContentHashMarkerKey, cancellationToken);
				if (string.Equals(existing, contentHash, StringComparison.Ordinal))
				{
					_logger.LogInformation("Knowledge reindex skipped (content hash unchanged: {Hash}).", contentHash);
					return new OperatorAiKnowledgeReindexResult(0, 0, _aiOptions.EmbeddingModel, Skipped: true, Coalesced: false, null);
				}
			}

			var documents = await BuildDocumentsAsync(cancellationToken);
			if (documents.Count == 0)
			{
				// Embedding failed for everything (worker down). Do NOT update the marker so a later run retries.
				return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: false, Coalesced: false, "embed_unavailable");
			}

			var request = new IndexKnowledgeRequest { CorrelationId = SearchWorkerKnowledgeClient.NewCorrelationId() };
			request.Documents.AddRange(documents);

			var response = await _knowledge.IndexKnowledgeAsync(request, cancellationToken);
			if (response is null)
				return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: false, Coalesced: false, "search_worker_unavailable");

			var embedModelVersion = documents[0].EmbedModelVersion;
			_logger.LogInformation(
				"Knowledge reindex complete: indexed={Indexed} failed={Failed} model={Model}",
				response.IndexedCount,
				response.FailedCount,
				embedModelVersion);

			// Persist the marker only when the upsert fully succeeded — partial failures should retry next boot.
			if (_redis is not null && response.FailedCount == 0 && documents.Count == OperatorAiEntityBundleCatalog.BundleCount)
				await _redis.SetWithExpiryMillisecondsAsync(ContentHashMarkerKey, contentHash, MarkerTtlMs, cancellationToken);

			return new OperatorAiKnowledgeReindexResult(
				response.IndexedCount,
				response.FailedCount,
				embedModelVersion,
				Skipped: false,
				Coalesced: false,
				response.FailedCount > 0 ? "partial_failure" : null);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Knowledge reindex failed");
			return new OperatorAiKnowledgeReindexResult(0, 0, null, Skipped: false, Coalesced: false, ex.Message);
		}
		finally
		{
			if (distributedLockHeld && _redis is not null)
			{
				try { await _redis.CompareAndDeleteAsync(ReindexLockKey, lockToken, CancellationToken.None); }
				catch (Exception ex) { _logger.LogWarning(ex, "Failed to release reindex distributed lock"); }
			}

			_inProgress = false;
			_gate.Release();
		}
	}

	/// <summary>
	/// Build one KnowledgeDocument per catalog bundle, embedding its content_text. Bundles whose embed fails are
	/// dropped (logged); when ALL fail we return an empty list so the caller reports embed_unavailable.
	/// </summary>
	private async Task<List<KnowledgeDocument>> BuildDocumentsAsync(CancellationToken cancellationToken)
	{
		var documents = new List<KnowledgeDocument>(OperatorAiEntityBundleCatalog.BundleCount);
		var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		foreach (var meta in OperatorAiEntityBundleCatalog.ListMetadata())
		{
			var contentText = BuildContentText(
				meta.Description,
				meta.Synonyms,
				meta.SampleQuestions);

			var embed = await _ai.EmbedTextAsync(contentText, _aiOptions.EmbeddingModel, cancellationToken);
			if (!embed.HasVector)
			{
				_logger.LogWarning(
					"Embed failed for bundle index {Index} ({Id}): {Error}",
					meta.Index,
					meta.Id,
					embed.Error);
				continue;
			}

			var doc = new KnowledgeDocument
			{
				KnowledgeId = meta.KnowledgeId,
				SourceType = StatBundleSourceType,
				BundleIndex = meta.Index,
				Title = meta.EntityName,
				Description = meta.Description,
				ContentText = contentText,
				VectorDim = embed.Vector!.Length,
				EmbedModelVersion = string.IsNullOrWhiteSpace(embed.ModelVersion) ? _aiOptions.EmbeddingModel : embed.ModelVersion,
				UpdatedAtUnixMs = nowMs,
			};
			doc.Synonyms.AddRange(meta.Synonyms);
			doc.SampleQuestions.AddRange(meta.SampleQuestions);
			doc.Vector.AddRange(embed.Vector!);
			documents.Add(doc);
		}

		return documents;
	}

	/// <summary>content_text = description + "\n" + synonyms.join(", ") + "\n" + sample_questions.join(" ") (§7.1).</summary>
	internal static string BuildContentText(string description, IReadOnlyList<string> synonyms, IReadOnlyList<string> sampleQuestions)
	{
		var sb = new StringBuilder();
		sb.Append(description.Trim());
		if (synonyms.Count > 0)
			sb.Append('\n').Append(string.Join(", ", synonyms));
		if (sampleQuestions.Count > 0)
			sb.Append('\n').Append(string.Join(' ', sampleQuestions));
		return sb.ToString();
	}

	/// <summary>Stable hash over {catalogVersion, all descriptor content_texts, embed_model_version} (§7.2).</summary>
	internal string ComputeContentHash()
	{
		var sb = new StringBuilder();
		sb.Append("v=").Append(OperatorAiEntityBundleCatalog.CatalogVersion).Append('|');
		sb.Append("model=").Append(_aiOptions.EmbeddingModel).Append('|');
		sb.Append("dim=").Append(_aiOptions.EmbeddingDim).Append('|');
		foreach (var meta in OperatorAiEntityBundleCatalog.ListMetadata())
		{
			sb.Append(meta.KnowledgeId).Append('=');
			sb.Append(BuildContentText(meta.Description, meta.Synonyms, meta.SampleQuestions));
			sb.Append(';');
		}

		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
		return Convert.ToHexString(bytes);
	}
}
