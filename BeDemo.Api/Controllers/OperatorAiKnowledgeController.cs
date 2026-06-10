using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Security;
using BeDemo.Api.Services.OperatorAi;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Operator AI RAG knowledge-index admin surface (operator-ai-rag-retrieval-refactor-v1, §7.2/§17.9).
///
/// <para>Two endpoints, both <c>CanManageAllFaces</c> (SUPER_ADMIN), under the existing <c>/api/operator-ai</c>
/// admin prefix used by the rest of the operator-AI settings controller:</para>
/// <list type="bullet">
///   <item><c>POST /api/operator-ai/knowledge/reindex</c> — force a full descriptor rebuild (§7.2 trigger 2),
///   returns <c>{ indexedCount, failedCount, embedModelVersion }</c>; 409 when a rebuild is already running
///   (single-flight, §17.5).</item>
///   <item><c>GET /api/operator-ai/knowledge/status</c> — proxies the worker <c>KnowledgeIndexStatus</c> via the
///   readiness cache for the admin health panel (§17.9).</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/operator-ai/knowledge")]
// Backend-refactor X5/X6: the SUPER_ADMIN operator gate (admin face scope + global SUPER_ADMIN) is enforced
// declaratively by the ManageAllFaces policy instead of the in-body RequireOperator() check. Same matrix (anonymous
// → 401, insufficient → 403, super-admin-in-admin-scope → allowed); pinned by the knowledge-controller integration
// negative test + PlatformSuperAdminAccessEdge.
[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
public sealed class OperatorAiKnowledgeController : ControllerBase
{
	private readonly IOperatorAiKnowledgeIndexer _indexer;
	private readonly IOperatorAiKnowledgeStatusCache _statusCache;
	private readonly IOperatorAiSystemSettingsProvider _systemSettings;
	private readonly ILogger<OperatorAiKnowledgeController> _logger;

	public OperatorAiKnowledgeController(
		IOperatorAiKnowledgeIndexer indexer,
		IOperatorAiKnowledgeStatusCache statusCache,
		IOperatorAiSystemSettingsProvider systemSettings,
		ILogger<OperatorAiKnowledgeController> logger)
	{
		_indexer = indexer;
		_statusCache = statusCache;
		_systemSettings = systemSettings;
		_logger = logger;
	}

	/// <summary>
	/// Force a full knowledge rebuild (§7.2/§8.1 "Reindex knowledge" button). Re-embeds all 61 descriptors and
	/// bulk-upserts them by <c>knowledge_id</c>. Returns the per-rebuild counts + the embed model version. When a
	/// rebuild (startup refresh or another admin) is already in flight, the single-flight gate coalesces and we
	/// surface HTTP 409 (§17.5) so the caller can retry once it finishes.
	/// </summary>
	[HttpPost("reindex")]
	[ProducesResponseType(typeof(KnowledgeReindexResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Reindex(CancellationToken cancellationToken)
	{
		// Authorization enforced by the ManageAllFaces policy on the controller.
		// Mirror the other operator-AI mutations: gate index work behind the global AI switch (RT-13).
		if (!await _systemSettings.IsAiEnabledAsync(cancellationToken))
			return Conflict(new ErrorResponseDto { Error = "Enable AI support in Settings before reindexing operator knowledge." });

		var result = await _indexer.RebuildAsync(force: true, cancellationToken);

		// Single-flight conflict → 409 (§17.5). The rebuild that won the lock keeps serving.
		if (result.Coalesced)
			return Conflict(new ErrorCodeResponseDto { Error = "A knowledge reindex is already running.", ErrorCode = "reindex_already_running" });

		// Worker/embed unavailable (e.g. search disabled, AI worker down) → 503; the admin can retry later.
		if (result.Error is not null && result.IndexedCount == 0 && !result.Skipped)
		{
			_logger.LogWarning("Operator AI knowledge reindex could not complete: {Error}", result.Error);
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new ErrorCodeResponseDto { Error = "Knowledge reindex could not complete.", ErrorCode = result.Error });
		}

		// Happy path (and idempotent skip): report the §7.2/§8.1 contract shape.
		return Ok(new KnowledgeReindexResultDto
		{
			IndexedCount = result.IndexedCount,
			FailedCount = result.FailedCount,
			EmbedModelVersion = result.EmbedModelVersion,
		});
	}

	/// <summary>
	/// Read-only knowledge-index health for the admin AI panel (§17.9). Proxies the worker
	/// <c>KnowledgeIndexStatus</c> through the readiness cache (bypassing the cache so the panel is fresh). 503 when
	/// the search worker is disabled/unreachable so the panel can show a degraded state.
	/// </summary>
	[HttpGet("status")]
	[ProducesResponseType(typeof(KnowledgeIndexStatusDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Status(CancellationToken cancellationToken)
	{
		// Authorization enforced by the ManageAllFaces policy on the controller.
		// forceRefresh: the admin panel wants the live worker status, not a possibly-stale readiness probe.
		var status = await _statusCache.GetStatusAsync(forceRefresh: true, cancellationToken);
		if (status is null)
		{
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new ErrorResponseDto { Error = "Knowledge index status unavailable (search worker disabled or unreachable)." });
		}

		// Project the proto message to a stable camelCase admin payload (doc count vs the expected 61, last-indexed
		// UTC, model, dim, ready/degraded) — §17.9 / RT-22.
		return Ok(new KnowledgeIndexStatusDto
		{
			Alias = status.Alias,
			ActiveIndex = status.ActiveIndex,
			DocCount = status.DocCount,
			ExpectedDocCount = status.ExpectedDocCount,
			EmbedModelVersion = status.EmbedModelVersion,
			VectorDim = status.VectorDim,
			Ready = status.Ready,
			Degraded = status.Degraded,
			LastIndexedUtc = status.LastIndexedUnixMs > 0
				? DateTimeOffset.FromUnixTimeMilliseconds(status.LastIndexedUnixMs).UtcDateTime
				: (DateTime?)null,
			RebuildInProgress = _indexer.IsRebuildInProgress,
			ErrorMessage = string.IsNullOrEmpty(status.ErrorMessage) ? null : status.ErrorMessage,
		});
	}
}
