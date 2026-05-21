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

    public int LiveTotalTimeoutSeconds { get; set; } = 300;

    public int LiveTimeseriesDays { get; set; } = 7;
}
