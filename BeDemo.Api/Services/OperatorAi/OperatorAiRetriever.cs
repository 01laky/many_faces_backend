using System.Diagnostics;
using System.Text.RegularExpressions;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Query-plane selection (§8). Replaces the LLM planner with embedding retrieval:
/// <c>EmbedText(message)</c> → <c>SemanticSearch(top_k, ["stat_bundle"])</c> → ordered bundle indices.
///
/// <para>Decision ladder (pipeline position = the "select" box of the unified orchestrator):</para>
/// 1. Readiness gate (§17.4): if the index is not ready (cold start / wrong dim / empty), short-circuit to the
///    legacy planner — never a zero-hit refusal due to an unbuilt index (RT-15).
/// 2. RAG: embed (with cache §17.8) + SemanticSearch; keep hits ≥ MinRetrievalScore. Embed/search timeout or
///    unavailability (§17.7) ⇒ planner fallback.
/// 3. Zero-hit escalation (§6.1): attempt 2 = planner; attempt 3 = relaxed retrieval (lower threshold, larger K);
///    else ZeroHit ⇒ the caller emits the fixed English refusal.
///
/// <para>Determinism:</para>
/// Hits are ordered by RRF score desc, then bundle_index asc; deduped; capped at MaxSelectedBundleIndices (RT-1).
///
/// <para>Inputs/outputs:</para>
/// message → <see cref="OperatorAiRetrievalResult"/> (indices + trace metadata). Pure selection — fresh values are
/// loaded later by the orchestrator; this never returns counts (correctness rule §4).
/// </summary>
public sealed class OperatorAiRetriever : IOperatorAiRetriever
{
	private const string StatBundleSourceType = "stat_bundle";

	private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

	private readonly IAiGrpcService _ai;
	private readonly ISearchWorkerKnowledgeClient _knowledge;
	private readonly IOperatorAiPlannerFallbackSelector _planner;
	private readonly IOperatorAiKnowledgeStatusCache _statusCache;
	private readonly IMemoryCache _memoryCache;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiRetriever> _logger;

	public OperatorAiRetriever(
		IAiGrpcService ai,
		ISearchWorkerKnowledgeClient knowledge,
		IOperatorAiPlannerFallbackSelector planner,
		IOperatorAiKnowledgeStatusCache statusCache,
		IMemoryCache memoryCache,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiRetriever> logger)
	{
		_ai = ai;
		_knowledge = knowledge;
		_planner = planner;
		_statusCache = statusCache;
		_memoryCache = memoryCache;
		_aiOptions = aiOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<OperatorAiRetrievalResult> RetrieveBundleIndicesAsync(string userMessage, CancellationToken cancellationToken = default)
	{
		var topK = Math.Max(1, _options.MaxSelectedBundleIndices);

		// (1) Cold-start readiness gate (§17.4 / RT-15): planner, not refusal, when the index is not usable.
		if (!_knowledge.IsAvailable || !await _statusCache.IsReadyAsync(cancellationToken))
		{
			_logger.LogInformation("Retriever: index not ready / worker unavailable → planner fallback.");
			return await PlannerFallbackAsync(userMessage, OperatorAiSelectionStrategy.Planner, embedMs: 0, searchMs: 0, embedCacheHit: false, degraded: true, cancellationToken);
		}

		// (2) RAG: embed (cached) then SemanticSearch.
		var (vector, embedMs, embedCacheHit) = await EmbedWithCacheAsync(userMessage, cancellationToken);
		if (vector is null)
		{
			// Embed unavailable / timed out (§17.7) → planner fallback.
			_logger.LogInformation("Retriever: embed unavailable → planner fallback.");
			return await PlannerFallbackAsync(userMessage, OperatorAiSelectionStrategy.Planner, embedMs, searchMs: 0, embedCacheHit, degraded: false, cancellationToken);
		}

		var (response, searchMs) = await SemanticSearchAsync(vector, userMessage, topK, _options.RetrievalRrfK, cancellationToken);
		if (response is null)
		{
			_logger.LogInformation("Retriever: SemanticSearch unavailable → planner fallback.");
			return await PlannerFallbackAsync(userMessage, OperatorAiSelectionStrategy.Planner, embedMs, searchMs, embedCacheHit, degraded: true, cancellationToken);
		}

		var usableHits = FilterAndOrderHits(response.Hits, _options.MinRetrievalScore, topK);
		if (usableHits.Count > 0)
		{
			return new OperatorAiRetrievalResult(
				usableHits.Select(h => h.BundleIndex).ToList(),
				OperatorAiSelectionStrategy.Rag,
				usableHits,
				response.Degraded,
				embedCacheHit,
				embedMs,
				searchMs);
		}

		// (3) Zero-hit escalation (§6.1). Attempt 2: planner over the catalog.
		var attempts = Math.Max(0, _options.ZeroHitRetryAttempts);
		if (attempts >= 1)
		{
			var plannerIndices = await _planner.SelectBundleIndicesAsync(userMessage, cancellationToken);
			if (plannerIndices.Count > 0)
			{
				return new OperatorAiRetrievalResult(
					CapAndDedupe(plannerIndices, topK),
					OperatorAiSelectionStrategy.Planner,
					Array.Empty<OperatorAiRetrievalHit>(),
					response.Degraded,
					embedCacheHit,
					embedMs,
					searchMs);
			}
		}

		// Attempt 3: relaxed retrieval (no score floor, larger K).
		if (attempts >= 2)
		{
			var relaxedK = Math.Min(OperatorAiEntityBundleCatalog.BundleCount, topK * 2);
			var (relaxed, relaxedMs) = await SemanticSearchAsync(vector, userMessage, relaxedK, _options.RetrievalRrfK, cancellationToken);
			searchMs += relaxedMs;
			if (relaxed is not null)
			{
				var relaxedHits = FilterAndOrderHits(relaxed.Hits, minScore: double.NegativeInfinity, topK);
				if (relaxedHits.Count > 0)
				{
					return new OperatorAiRetrievalResult(
						relaxedHits.Select(h => h.BundleIndex).ToList(),
						OperatorAiSelectionStrategy.Relaxed,
						relaxedHits,
						relaxed.Degraded,
						embedCacheHit,
						embedMs,
						searchMs);
				}
			}
		}

		// Still nothing usable → ZeroHit (caller emits the fixed English refusal).
		_logger.LogInformation("Retriever: zero-hit after {Attempts} escalation attempts.", attempts);
		return new OperatorAiRetrievalResult(
			Array.Empty<int>(),
			OperatorAiSelectionStrategy.ZeroHit,
			Array.Empty<OperatorAiRetrievalHit>(),
			response.Degraded,
			embedCacheHit,
			embedMs,
			searchMs);
	}

	// ── helpers ────────────────────────────────────────────────────────────────

	/// <summary>Run the planner fallback selector and wrap the result. Empty planner output ⇒ ZeroHit.</summary>
	private async Task<OperatorAiRetrievalResult> PlannerFallbackAsync(
		string userMessage,
		OperatorAiSelectionStrategy strategy,
		long embedMs,
		long searchMs,
		bool embedCacheHit,
		bool degraded,
		CancellationToken cancellationToken)
	{
		var indices = await _planner.SelectBundleIndicesAsync(userMessage, cancellationToken);
		if (indices.Count == 0)
		{
			return new OperatorAiRetrievalResult(
				Array.Empty<int>(),
				OperatorAiSelectionStrategy.ZeroHit,
				Array.Empty<OperatorAiRetrievalHit>(),
				degraded,
				embedCacheHit,
				embedMs,
				searchMs);
		}

		return new OperatorAiRetrievalResult(
			CapAndDedupe(indices, Math.Max(1, _options.MaxSelectedBundleIndices)),
			strategy,
			Array.Empty<OperatorAiRetrievalHit>(),
			degraded,
			embedCacheHit,
			embedMs,
			searchMs);
	}

	/// <summary>Embed the message with the IMemoryCache (§17.8): normalize+model-version key, TTL, bounded size.</summary>
	private async Task<(float[]? Vector, long EmbedMs, bool CacheHit)> EmbedWithCacheAsync(string userMessage, CancellationToken cancellationToken)
	{
		var key = BuildEmbedCacheKey(userMessage, _aiOptions.EmbeddingModel);
		if (_memoryCache.TryGetValue<float[]>(key, out var cached) && cached is { Length: > 0 })
			return (cached, 0, true);

		var sw = Stopwatch.StartNew();
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(Math.Max(500, _options.EmbedTimeoutMs));

		AiEmbedTextResult embed;
		try
		{
			embed = await _ai.EmbedTextAsync(userMessage, _aiOptions.EmbeddingModel, cts.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			sw.Stop();
			_logger.LogWarning("Embed timed out after {Ms}ms.", _options.EmbedTimeoutMs);
			return (null, sw.ElapsedMilliseconds, false);
		}

		sw.Stop();
		if (!embed.HasVector)
			return (null, sw.ElapsedMilliseconds, false);

		_memoryCache.Set(key, embed.Vector!, new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.QueryEmbeddingCacheTtlSeconds)),
			Size = 1,
		});

		return (embed.Vector, sw.ElapsedMilliseconds, false);
	}

	/// <summary>Run SemanticSearch with the retrieval timeout (§17.7). Null ⇒ unavailable/timeout ⇒ planner fallback.</summary>
	private async Task<(SemanticSearchResponse? Response, long SearchMs)> SemanticSearchAsync(
		float[] vector,
		string queryText,
		int topK,
		int rrfK,
		CancellationToken cancellationToken)
	{
		var request = new SemanticSearchRequest
		{
			QueryText = queryText,
			TopK = topK,
			RrfK = rrfK,
		};
		request.QueryVector.AddRange(vector);
		request.SourceTypes.Add(StatBundleSourceType);

		var sw = Stopwatch.StartNew();
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(Math.Max(500, _options.RetrievalTimeoutMs));

		try
		{
			var response = await _knowledge.SemanticSearchAsync(request, cts.Token);
			sw.Stop();
			return (response, sw.ElapsedMilliseconds);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			sw.Stop();
			_logger.LogWarning("SemanticSearch timed out after {Ms}ms.", _options.RetrievalTimeoutMs);
			return (null, sw.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			sw.Stop();
			_logger.LogWarning(ex, "SemanticSearch failed.");
			return (null, sw.ElapsedMilliseconds);
		}
	}

	/// <summary>Keep hits ≥ minScore, order by score desc then bundle_index asc, dedupe, cap at K (RT-1).</summary>
	internal static List<OperatorAiRetrievalHit> FilterAndOrderHits(IEnumerable<SemanticSearchHit> hits, double minScore, int topK)
	{
		var seen = new HashSet<int>();
		var ordered = hits
			.Where(h => h.SourceType == StatBundleSourceType && h.BundleIndex >= 0 && h.Score >= minScore)
			.OrderByDescending(h => h.Score)
			.ThenBy(h => h.BundleIndex)
			.Select(h => new OperatorAiRetrievalHit(h.KnowledgeId, h.BundleIndex, h.Score, h.VectorRank, h.TextRank));

		var result = new List<OperatorAiRetrievalHit>(topK);
		foreach (var hit in ordered)
		{
			if (!seen.Add(hit.BundleIndex))
				continue;
			result.Add(hit);
			if (result.Count >= topK)
				break;
		}

		return result;
	}

	/// <summary>Dedupe + cap an index list (used for planner output) preserving order.</summary>
	internal static List<int> CapAndDedupe(IReadOnlyList<int> indices, int topK)
	{
		var seen = new HashSet<int>();
		var result = new List<int>(topK);
		foreach (var idx in indices)
		{
			if (idx < 0 || idx >= OperatorAiEntityBundleCatalog.BundleCount)
				continue;
			if (!seen.Add(idx))
				continue;
			result.Add(idx);
			if (result.Count >= topK)
				break;
		}

		return result;
	}

	/// <summary>normalize = trim + lowercase + collapse whitespace; key includes the embed model version (§17.8).</summary>
	internal static string BuildEmbedCacheKey(string message, string embedModel)
	{
		var normalized = WhitespaceRegex.Replace(message.Trim().ToLowerInvariant(), " ");
		return $"operator-ai:qemb:{embedModel}:{normalized}";
	}
}
