namespace BeDemo.Api.Configuration;

/// <summary>Operator AI chat (shared support inbox) — see docs/guides/admin-operator-ai-chat-threads.md.</summary>
public sealed class OperatorAiOptions
{
	public const string SectionName = "OperatorAi";

	public int MaxHistoryPairs { get; set; } = 5;
	public int MaxMessageLength { get; set; } = 16_000;
	public int MaxConversationsListPageSize { get; set; } = 50;
	public int MaxConversations { get; set; } = 1000;
	public int MessagesPageSize { get; set; } = 40;
	public int MaxNewTokens { get; set; } = 2048;

	/// <summary>When true, attach stats JSON only if the user message looks metrics-related (recommended).</summary>
	public bool AttachStatsOnlyForMetricsQuestions { get; set; } = true;

	/// <summary>When stats are attached, include last-7-days daily buckets for users/messages/stories.</summary>
	public bool IncludeTimeseriesInStatsContext { get; set; } = true;

	// Live map-reduce (statsMode=live) — see docs/prompts/admin-operator-ai-live-stats-bundle-map-reduce-agent-prompt.md

	/// <summary>Max concurrent per-bundle AI Generate calls (server-side hard cap).</summary>
	public int MaxParallelBundleAiCalls { get; set; } = 2;

	/// <summary>Planner may select at most this many bundle indices per message.</summary>
	public int MaxSelectedBundleIndices { get; set; } = 4;

	public int LiveBundleCatalogVersion { get; set; } = 2;

	public int LivePrefetchTimeoutSeconds { get; set; } = 60;

	public int LivePlannerMaxNewTokens { get; set; } = 128;

	public int LiveBundleMaxNewTokens { get; set; } = 256;

	/// <summary>Final AI pass that turns stitched bundle facts into one operator-friendly reply.</summary>
	public bool LiveUseAiSynthesisStitch { get; set; } = true;

	public int LiveStitchMaxNewTokens { get; set; } = 512;

	public int LiveTotalTimeoutSeconds { get; set; } = 300;

	public int LiveTimeseriesDays { get; set; } = 7;

	// Live stats Redis bundle cache — see docs/prompts/operator-ai-live-stats-redis-cache-agent-prompt.md

	/// <summary>Fallback TTL when PostgreSQL singleton row is missing (milliseconds).</summary>
	public long LiveBundleCacheTtlMilliseconds { get; set; } = OperatorAiLiveStatsCacheConstraints.DefaultTtlMilliseconds;

	/// <summary>Single-flight lock safety TTL (seconds).</summary>
	public int LiveBundleCacheLockSeconds { get; set; } = 30;

	/// <summary>Waiter poll interval while another worker holds the bundle lock (milliseconds).</summary>
	public int LiveBundleCacheWaitPollMilliseconds { get; set; } = 50;

	/// <summary>L1 in-process cache duration for TTL settings reads (seconds).</summary>
	public int LiveBundleCacheSettingsMemoryCacheSeconds { get; set; } = 30;

	/// <summary>When true, prefetch all bundles into Redis shortly after backend start.</summary>
	public bool WarmLiveBundleCacheOnStartup { get; set; }

	/// <summary>Delay before startup warm begins (seconds).</summary>
	public int WarmLiveBundleCacheStartupDelaySeconds { get; set; } = 5;

	/// <summary>Max wall time for startup warm prefetch (seconds).</summary>
	public int WarmLiveBundleCacheStartupTimeoutSeconds { get; set; } = 120;

	// ── RAG retrieval refactor v1 (operator-ai-rag-retrieval-refactor-v1) ──────
	// Embedding-based semantic retrieval replaces the LLM planner as the bundle
	// SELECTION step; the per-bundle map + stitch is retained. The planner is now
	// only the degraded fallback selector (embed/ES down or index not ready).

	/// <summary>§6.1 — extra selection attempts after a zero-hit RAG result (planner, then relaxed retrieval) before the fixed refusal. Default 2.</summary>
	public int ZeroHitRetryAttempts { get; set; } = 2;

	/// <summary>§4/§6 — minimum fused RRF score for a SemanticSearch hit to count as usable (below ⇒ zero-hit escalation). Tune during impl.</summary>
	public double MinRetrievalScore { get; set; }

	/// <summary>Skills §4/§6 — minimum cosine similarity for the top skill to be selected; below ⇒ general-assistant fallback. Default 0.35; tune during impl.</summary>
	public double SkillRoutingMinScore { get; set; } = 0.35;

	/// <summary>§17.4 — TTL (seconds) for the cached KnowledgeIndexStatus readiness probe that gates the planner fallback on cold start.</summary>
	public int KnowledgeStatusCacheTtlSeconds { get; set; } = 15;

	/// <summary>§17.8 — query-embedding cache TTL (seconds); keyed by normalize(message)+embed_model_version. Default 300.</summary>
	public int QueryEmbeddingCacheTtlSeconds { get; set; } = 300;

	/// <summary>§17.8 — bounded entry count for the query-embedding IMemoryCache.</summary>
	public int QueryEmbeddingCacheMaxEntries { get; set; } = 512;

	/// <summary>§17.7 — embed call budget (ms); on timeout ⇒ planner fallback.</summary>
	public int EmbedTimeoutMs { get; set; } = 8_000;

	/// <summary>§17.7 — SemanticSearch budget (ms); on timeout ⇒ planner fallback.</summary>
	public int RetrievalTimeoutMs { get; set; } = 8_000;

	/// <summary>§17.7 — per-bundle Generate budget (ms); on timeout ⇒ drop that section + one-line note.</summary>
	public int PerBundleGenerateTimeoutMs { get; set; } = 60_000;

	/// <summary>§17.7 — overall turn budget (ms); exceeded ⇒ stitch whatever is ready + a note. Never hang.</summary>
	public int OverallTurnBudgetMs { get; set; } = 240_000;

	/// <summary>§17.1 — emit the per-turn structured retrieval trace (selected ids, RRF scores, stage latencies). Dev:true / prod:false.</summary>
	public bool RetrievalTraceEnabled { get; set; } = true;

	/// <summary>§17.10 — dev-only "why these bundles" debug payload alongside the assistant message. Off by default.</summary>
	public bool LiveStatsDebugJson { get; set; }

	/// <summary>RRF constant passed to the worker SemanticSearch (0 ⇒ worker default 60).</summary>
	public int RetrievalRrfK { get; set; }

	// Global AI master switch — see docs/prompts/admin-global-ai-enable-switch-agent-prompt.md

	/// <summary>Bootstrap default when inserting the singleton row for the first time (Testing always false).</summary>
	public bool DefaultAiEnabled { get; set; }

	/// <summary>Max seconds to poll model Loading during Activate AI before failing enable.</summary>
	public int EnableHealthLoadingWaitSeconds { get; set; } = 30;

	/// <summary>Delay between GetModelStatus polls while model is Loading during Activate AI.</summary>
	public int EnableHealthPollIntervalSeconds { get; set; } = 2;
}
