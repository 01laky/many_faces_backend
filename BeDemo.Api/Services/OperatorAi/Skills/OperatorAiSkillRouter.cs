using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>Result of routing one operator message: the chosen skill + the cosine score that picked it.</summary>
public sealed record OperatorAiSkillRoute(IOperatorAiSkill Skill, double Score, bool Fallback);

/// <summary>
/// Picks the single skill for an operator message (D2/D3, §4). v1 routes by **in-memory cosine** over the 4 skill
/// descriptor vectors (cached in a singleton, <see cref="IOperatorAiSkillVectorCache"/>) — the
/// `operator-ai-knowledge` index stays bundles-only (no Go-worker change for 4 skills).
/// </summary>
public interface IOperatorAiSkillRouter
{
	Task<OperatorAiSkillRoute> RouteAsync(string userMessage, CancellationToken cancellationToken = default);

	/// <summary>7B-perf O8 — warm the 4 descriptor vectors ahead of the first turn (startup). True when warmed.</summary>
	Task<bool> WarmAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiSkillRouter : IOperatorAiSkillRouter
{
	private readonly IOperatorAiSkillRegistry _registry;
	private readonly IOperatorAiSkillVectorCache _vectorCache;
	private readonly IAiGrpcService _ai;
	private readonly IMemoryCache _memoryCache;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiSkillRouter> _logger;

	public OperatorAiSkillRouter(
		IOperatorAiSkillRegistry registry,
		IOperatorAiSkillVectorCache vectorCache,
		IAiGrpcService ai,
		IMemoryCache memoryCache,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiSkillRouter> logger)
	{
		_registry = registry;
		_vectorCache = vectorCache;
		_ai = ai;
		_memoryCache = memoryCache;
		_aiOptions = aiOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> WarmAsync(CancellationToken cancellationToken = default)
	{
		var descriptors = _registry.All
			.Select(s => (s.Id, Text: OperatorAiSkillRouter.BuildDescriptorText(s)))
			.ToList();
		var vectors = await _vectorCache.GetOrWarmAsync(descriptors, _aiOptions.EmbeddingModel, EmbedAsync, cancellationToken);
		return vectors is { Count: > 0 };
	}

	/// <inheritdoc />
	public async Task<OperatorAiSkillRoute> RouteAsync(string userMessage, CancellationToken cancellationToken = default)
	{
		var fallback = _registry.GeneralAssistant;

		var descriptors = _registry.All
			.Select(s => (s.Id, Text: OperatorAiSkillRouter.BuildDescriptorText(s)))
			.ToList();

		var vectors = await _vectorCache.GetOrWarmAsync(descriptors, _aiOptions.EmbeddingModel, EmbedAsync, cancellationToken);
		if (vectors is null || vectors.Count == 0)
		{
			// Embedding unavailable (worker down / model not ready) → general-assistant, never a hard refusal (Q6/D5).
			_logger.LogInformation("Skill router: descriptor vectors unavailable → general-assistant fallback.");
			return new OperatorAiSkillRoute(fallback, 0, Fallback: true);
		}

		var query = await EmbedQueryAsync(userMessage, cancellationToken);
		if (query is null)
		{
			_logger.LogInformation("Skill router: query embed unavailable → general-assistant fallback.");
			return new OperatorAiSkillRoute(fallback, 0, Fallback: true);
		}

		string? bestId = null;
		var bestScore = double.NegativeInfinity;
		foreach (var (id, vec) in vectors)
		{
			var score = Dot(query, vec); // both unit-normalized → cosine similarity
			if (score > bestScore)
			{
				bestScore = score;
				bestId = id;
			}
		}

		// Below the routing threshold ("no skill matches") → general-assistant (Q6/D5).
		if (bestId is null || bestScore < _options.SkillRoutingMinScore)
			return new OperatorAiSkillRoute(fallback, double.IsNegativeInfinity(bestScore) ? 0 : bestScore, Fallback: true);

		var skill = _registry.GetById(bestId) ?? fallback;
		return new OperatorAiSkillRoute(skill, bestScore, Fallback: skill.Id == fallback.Id);
	}

	/// <summary>Descriptor text mirrors the bundle convention: description + sample requests.</summary>
	internal static string BuildDescriptorText(IOperatorAiSkill skill) =>
		$"{skill.Description}\n{string.Join(" ", skill.SampleRequests)}".Trim();

	/// <summary>Embed one string and L2-normalize it (so a dot product equals cosine similarity). Null on failure/timeout.</summary>
	private async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(500, _options.EmbedTimeoutMs)));
		try
		{
			var embed = await _ai.EmbedTextAsync(text, _aiOptions.EmbeddingModel, cts.Token);
			return embed.HasVector ? Normalize(embed.Vector!) : null;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("Skill router embed timed out after {Ms}ms.", _options.EmbedTimeoutMs);
			return null;
		}
	}

	/// <summary>
	/// 7B-perf O8 — embed the OPERATOR QUESTION once per turn. The router embeds it for cosine routing; the stats
	/// retriever needs the same embedding for kNN. We populate the retriever's shared query-embedding cache with the
	/// RAW vector under its exact key, so when the stats skill runs the retriever gets a cache HIT instead of a second
	/// EmbedText round-trip. The router still uses a normalized copy for cosine.
	/// </summary>
	private async Task<float[]?> EmbedQueryAsync(string userMessage, CancellationToken cancellationToken)
	{
		var key = OperatorAiRetriever.BuildEmbedCacheKey(userMessage, _aiOptions.EmbeddingModel);
		float[]? raw;

		if (_memoryCache.TryGetValue<float[]>(key, out var cachedRaw) && cachedRaw is { Length: > 0 })
		{
			raw = cachedRaw;
		}
		else
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(500, _options.EmbedTimeoutMs)));
			try
			{
				var embed = await _ai.EmbedTextAsync(userMessage, _aiOptions.EmbeddingModel, cts.Token);
				raw = embed.HasVector ? embed.Vector : null;
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("Skill router query embed timed out after {Ms}ms.", _options.EmbedTimeoutMs);
				return null;
			}

			if (raw is { Length: > 0 })
			{
				// Seed the retriever's cache (raw, un-normalized) so the stats skill embeds the message zero times.
				_memoryCache.Set(key, raw, new MemoryCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.QueryEmbeddingCacheTtlSeconds)),
					Size = 1,
				});
			}
		}

		return raw is { Length: > 0 } ? Normalize(raw) : null;
	}

	internal static float[] Normalize(float[] v)
	{
		double sum = 0;
		for (var i = 0; i < v.Length; i++)
			sum += (double)v[i] * v[i];
		var norm = Math.Sqrt(sum);
		if (norm <= 1e-12)
			return v;
		var outv = new float[v.Length];
		for (var i = 0; i < v.Length; i++)
			outv[i] = (float)(v[i] / norm);
		return outv;
	}

	internal static double Dot(float[] a, float[] b)
	{
		var n = Math.Min(a.Length, b.Length);
		double sum = 0;
		for (var i = 0; i < n; i++)
			sum += (double)a[i] * b[i];
		return sum;
	}
}
