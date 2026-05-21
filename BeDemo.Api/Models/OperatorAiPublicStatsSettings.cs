using BeDemo.Api.Configuration;

namespace BeDemo.Api.Models;

/// <summary>
/// Singleton row for global operator AI public stats chat settings (mode + live parallel cap).
/// </summary>
public class OperatorAiPublicStatsSettings
{
    /// <summary>Always <c>1</c> — platform-wide singleton.</summary>
    public int Id { get; set; } = 1;

    /// <summary><c>off</c>, <c>inline</c>, or <c>live</c>.</summary>
    public string PublicStatsMode { get; set; } = OperatorAiPublicStatsConstraints.DefaultPublicStatsMode;

    public int LiveMaxParallelBundleCalls { get; set; } =
        OperatorAiPublicStatsConstraints.DefaultLiveMaxParallelBundleCalls;

    public DateTime UpdatedAtUtc { get; set; }

    public string? UpdatedByUserId { get; set; }
}
