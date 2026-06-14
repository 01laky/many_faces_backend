using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
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
	/// <summary>Score stamped on a route chosen by the deterministic broad pre-route or the 3B helper (not cosine).</summary>
	private const double NonCosineRouteScore = 1.0;

	private readonly IOperatorAiSkillRegistry _registry;
	private readonly IOperatorAiSkillVectorCache _vectorCache;
	private readonly IOperatorAiDecisionHelper _decisions;
	private readonly IAiGrpcService _ai;
	private readonly IMemoryCache _memoryCache;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiSkillRouter> _logger;

	public OperatorAiSkillRouter(
		IOperatorAiSkillRegistry registry,
		IOperatorAiSkillVectorCache vectorCache,
		IOperatorAiDecisionHelper decisions,
		IAiGrpcService ai,
		IMemoryCache memoryCache,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiSkillRouter> logger)
	{
		_registry = registry;
		_vectorCache = vectorCache;
		_decisions = decisions;
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

		// ── (1) Deterministic broad pre-route → stats (operator-ai LLM skill router §3.1) ───────────────────────────
		// The ONLY safe keyword pre-route: "full/all entity statistics", "overview of everything", … are unambiguously
		// the stats skill. We do this BEFORE the helper so a whole-platform overview never gets mis-classified into
		// reports/moderation by a weak 3B (this is exactly the "Give me full entities statistics" regression). The
		// helper's broad UPGRADE of a keyword-miss is a stats-internal concern (mapping all 61 bundles), not routing.
		if (OperatorAiStatsIntent.IsBroadOverviewQuestion(userMessage))
		{
			var statsSkill = _registry.GetById("stats");
			if (statsSkill is not null)
			{
				_logger.LogInformation("Skill router: broad-overview keyword pre-route → stats.");
				return new OperatorAiSkillRoute(statsSkill, NonCosineRouteScore, Fallback: false);
			}
		}

		// ── (2) 3B helper LLM classification (operator-ai LLM skill router §3.2) ────────────────────────────────────
		// A tiny single-label classification over the terse per-skill RouterHints decides reports-vs-stats-vs-… far
		// more reliably than descriptor cosine. Runs BEFORE the embed early-returns (D.1) so it still routes when the
		// embedding worker/model is unavailable. Helper off / unparseable / errored ⇒ null ⇒ fall through to cosine.
		var candidates = _registry.All
			.Select(s => (Id: s.Id, Label: RouterLabel(s.Id), Hint: s.RouterHint))
			.ToList();
		var helperId = await _decisions.DetectSkillAsync(userMessage, candidates, cancellationToken);
		if (helperId is not null)
		{
			var helperSkill = _registry.GetById(helperId);
			if (helperSkill is not null)
			{
				_logger.LogInformation("Skill router: 3B helper → {SkillId}.", helperSkill.Id);
				return new OperatorAiSkillRoute(helperSkill, NonCosineRouteScore, Fallback: helperSkill.Id == fallback.Id);
			}
		}

		// ── (3) Cosine fallback over the descriptor vectors (operator-ai LLM skill router §3.3) ─────────────────────
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

	/// <summary>
	/// Single-token label fed to the 3B router for a skill id, so a weak helper does not have to reproduce a hyphenated
	/// id like "general-assistant" (it answers "general" and we map it back). Data-skill ids are already single tokens.
	/// </summary>
	internal static string RouterLabel(string id) =>
		id == OperatorAiSkillRegistry.GeneralAssistantId ? "general" : id;

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
