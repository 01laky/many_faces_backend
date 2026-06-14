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

	/// <summary>
	/// Max concurrent per-bundle AI Generate calls (server-side hard cap). Default 1 (7B-perf O13): on a single
	/// Ollama instance without OLLAMA_NUM_PARALLEL the map calls serialize anyway, so &gt;1 only adds scheduling
	/// overhead and (on a VRAM-constrained GPU) reduces offload per request. Raise only on a host that runs
	/// concurrent model slots. Speed comes from FEWER calls (O2/O3) + SHORTER calls (O7/O11/O12), not concurrency.
	/// </summary>
	public int MaxParallelBundleAiCalls { get; set; } = 1;

	/// <summary>Planner may select at most this many bundle indices per message.</summary>
	public int MaxSelectedBundleIndices { get; set; } = 4;

	/// <summary>
	/// Full-stats broad-overview fix: parallelism for the per-bundle maps on the broad-overview path (all 61
	/// bundles). Used directly as the map gate size — deliberately NOT clamped by
	/// <see cref="MaxParallelBundleAiCalls"/> (which pins the focused path to 1), since the broad path runs the
	/// 61 tiny maps on the CPU helper and would otherwise be serial. Raise on a host with concurrent model slots.
	/// </summary>
	public int BroadOverviewMaxParallel { get; set; } = 4;

	/// <summary>
	/// Full-stats broad-overview: when true (default), the whole-platform snapshot renders each of the 61 entity
	/// bundles DETERMINISTICALLY from its loaded counts (<see cref="OperatorAiCountFastPath"/>) with ZERO Generate
	/// calls, instead of one per-bundle LLM "map" each. A broad overview is a pure counts read-out, so the LLM adds
	/// nothing while 61 maps on a CPU helper never fit the turn budget (they all time out → empty answer). Set false
	/// to restore the per-bundle LLM map for the broad path (only sensible on a host that can run 61 maps in budget).
	/// </summary>
	public bool LiveBroadDeterministicCounts { get; set; } = true;

	public int LiveBundleCatalogVersion { get; set; } = 2;

	public int LivePrefetchTimeoutSeconds { get; set; } = 60;

	public int LivePlannerMaxNewTokens { get; set; } = 128;

	/// <summary>
	/// Per-bundle MAP generation token cap. Default 96 (7B-perf O7): map answers are terse facts, so a tight cap
	/// makes each of the K map calls faster. The operator-visible synthesis / single-bundle answer keep the larger
	/// <see cref="LiveStitchMaxNewTokens"/> budget.
	/// </summary>
	public int LiveBundleMaxNewTokens { get; set; } = 96;

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

	/// <summary>§17.7 / 7B-perf O15 — per-bundle Generate budget (ms); on timeout ⇒ drop that section + one-line note. Default 30s: with the 96-token map cap (O7) a bundle answers well within this, while a hung worker still aborts.</summary>
	public int PerBundleGenerateTimeoutMs { get; set; } = 30_000;

	/// <summary>§17.7 / 7B-perf O15 — overall turn budget (ms); exceeded ⇒ stitch whatever is ready + a note. Never hang. Default 180s ≈ the measured 90–120s baseline plus headroom, so a correct-but-slow turn completes.</summary>
	public int OverallTurnBudgetMs { get; set; } = 180_000;

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

	// ── 7B performance optimizations v1 (operator-ai-7b-performance-v1) ─────────
	// Speed up the operator chat on a local 7B: fewer generations (count / single-bundle
	// fast-paths), faster generations (lower map sampling), streaming, a single-flight
	// guard, an optional answer cache, and an optional CPU-resident helper model.

	/// <summary>O11 — sampling temperature for the per-bundle MAP Generate (facts only). Low ⇒ terse + deterministic. Default 0.2. The operator-visible synthesis keeps the worker's default sampling.</summary>
	public double MapTemperature { get; set; } = 0.2;

	/// <summary>O11 — stop sequences for the MAP Generate so it stops early instead of padding to the token cap or drifting into a new turn.</summary>
	public string[] MapStopSequences { get; set; } = ["\nUser:", "\nQuestion:", "\n\n\n"];

	/// <summary>O4 — wire the worker GenerateStream so the operator-visible answer streams token-by-token. When false (or the worker can't stream), the non-streaming Generate path is used. Default true.</summary>
	public bool StreamingEnabled { get; set; } = true;

	/// <summary>O17 — reject a second concurrent operator turn for the same conversation while one is still generating (the GPU is a serial resource). Default true.</summary>
	public bool SingleActiveGenerationGuardEnabled { get; set; } = true;

	/// <summary>O18 — optional short-TTL exact-repeat answer cache (skip the whole turn on an identical repeat). Off by default; the freshness window must align with the bundle cache.</summary>
	public bool AnswerCacheEnabled { get; set; }

	/// <summary>O18 — TTL (seconds) for the answer cache; keep ≤ the bundle-cache freshness window so a cached count never outlives the live data.</summary>
	public int AnswerCacheTtlSeconds { get; set; } = 30;

	/// <summary>O18 — bounded entry count for the answer cache.</summary>
	public int AnswerCacheMaxEntries { get; set; } = 256;

	/// <summary>O19 Role A — let the CPU-resident helper model (when <see cref="AiServiceOptions.HelperModel"/> is set) make the small routing/gating decisions (simple-count gate, report-type). Falls back to the deterministic heuristic when the helper is unset/unavailable. Default true (no-op unless a helper model is configured).</summary>
	public bool HelperForDecisions { get; set; } = true;

	/// <summary>O19 Role B — experimental heterogeneous parallel map (split bundles across the GPU 7B and the CPU helper). Off by default; enable only if the O9 benchmark shows a net win at acceptable quality.</summary>
	public bool HelperParallelMapEnabled { get; set; }
}
