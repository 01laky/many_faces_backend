using BeDemo.Api.Configuration;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Short-TTL cache over the worker <c>KnowledgeIndexStatus</c> RPC backing the cold-start readiness gate (§17.4)
/// and the admin status panel (§17.9). Caching avoids a worker round trip on every operator turn while keeping
/// readiness fresh enough to flip the retriever back from planner-fallback to RAG once the index is built.
/// </summary>
public interface IOperatorAiKnowledgeStatusCache
{
	/// <summary>True when the index is usable for retrieval (ready flag AND model/dim match config). Cached.</summary>
	Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

	/// <summary>Fetch the raw status (admin panel). <paramref name="forceRefresh"/> bypasses the cache. Null ⇒ worker unavailable.</summary>
	Task<KnowledgeIndexStatusResponse?> GetStatusAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiKnowledgeStatusCache : IOperatorAiKnowledgeStatusCache
{
	private const string CacheKey = "operator-ai:knowledge-status";

	private readonly ISearchWorkerKnowledgeClient _knowledge;
	private readonly IMemoryCache _cache;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiKnowledgeStatusCache> _logger;

	public OperatorAiKnowledgeStatusCache(
		ISearchWorkerKnowledgeClient knowledge,
		IMemoryCache cache,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiKnowledgeStatusCache> logger)
	{
		_knowledge = knowledge;
		_cache = cache;
		_aiOptions = aiOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
	{
		var status = await GetStatusAsync(forceRefresh: false, cancellationToken);
		if (status is null)
			return false;

		// Readiness = worker ready flag AND the deployed model + dim match our single source of truth (§5.5/§17.4).
		// A model/dim drift means the index is stale for our config ⇒ treat as not ready ⇒ planner fallback.
		var modelMatches = string.IsNullOrEmpty(status.EmbedModelVersion)
			|| string.Equals(status.EmbedModelVersion, _aiOptions.EmbeddingModel, StringComparison.OrdinalIgnoreCase);
		var dimMatches = status.VectorDim == 0 || status.VectorDim == _aiOptions.EmbeddingDim;

		return status.Ready && modelMatches && dimMatches;
	}

	/// <inheritdoc />
	public async Task<KnowledgeIndexStatusResponse?> GetStatusAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
	{
		if (!forceRefresh && _cache.TryGetValue<KnowledgeIndexStatusResponse>(CacheKey, out var cached))
			return cached;

		if (!_knowledge.IsAvailable)
			return null;

		try
		{
			var status = await _knowledge.KnowledgeIndexStatusAsync(new KnowledgeIndexStatusRequest(), cancellationToken);
			if (status is not null)
			{
				_cache.Set(CacheKey, status, new MemoryCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.KnowledgeStatusCacheTtlSeconds)),
					Size = 1,
				});
			}

			return status;
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "KnowledgeIndexStatus probe failed.");
			return null;
		}
	}
}
