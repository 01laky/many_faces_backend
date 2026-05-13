namespace BeDemo.Api.Services;

/// <summary>Configuration for untrusted-content defenses on the path to <c>ReviewContent</c> (gRPC).</summary>
public sealed class ContentModerationSecurityOptions
{
    public const string SectionName = "ContentModeration";

    /// <summary>
    /// When true, title/body (as stored for the submission) are scanned for instruction-like patterns;
    /// any <see cref="AiReviewDecision.Approve"/> from the AI is downgraded to human review after validation.
    /// </summary>
    public bool InstructionHeuristicEnabled { get; set; } = true;
}
