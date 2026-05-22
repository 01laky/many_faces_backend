namespace BeDemo.Api.Models;

/// <summary>
/// Singleton platform row: global on/off for all AI inference (operator chat, face chat AI, content moderation AI).
/// Default is disabled until an operator explicitly activates AI in admin Settings.
/// </summary>
public class OperatorAiSystemSettings
{
    /// <summary>Always <c>1</c> — platform-wide singleton.</summary>
    public int Id { get; set; } = 1;

    /// <summary>When false, no gRPC inference runs; moderation falls back to human review.</summary>
    public bool AiEnabled { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string? UpdatedByUserId { get; set; }

    public DateTime? LastEnabledAtUtc { get; set; }

    /// <summary>Last enable health outcome, e.g. <c>ok</c>, <c>model_loading_timeout</c>, <c>worker_unreachable</c>.</summary>
    public string? LastEnableHealthStatus { get; set; }
}
