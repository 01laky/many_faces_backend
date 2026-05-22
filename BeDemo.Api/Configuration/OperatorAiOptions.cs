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

    // Global AI master switch — see docs/prompts/admin-global-ai-enable-switch-agent-prompt.md

    /// <summary>Bootstrap default when inserting the singleton row for the first time (Testing always false).</summary>
    public bool DefaultAiEnabled { get; set; }

    /// <summary>Max seconds to poll model Loading during Activate AI before failing enable.</summary>
    public int EnableHealthLoadingWaitSeconds { get; set; } = 30;

    /// <summary>Delay between GetModelStatus polls while model is Loading during Activate AI.</summary>
    public int EnableHealthPollIntervalSeconds { get; set; } = 2;
}
