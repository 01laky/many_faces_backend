namespace BeDemo.Api.Services;

/// <summary>
/// Lightweight, high-recall / low-precision detector for instruction-like phrases in user submissions.
/// A positive match must never auto-approve: <see cref="ContentModerationHelpers.ValidateRecommendation"/> forces human review when combined with <see cref="AiReviewDecision.Approve"/>.
/// </summary>
public static class ContentModerationPromptInjectionHeuristic
{
    /// <summary>Stored on <c>AiReviewFlagsJson</c> when the heuristic fires (also whitelisted in <see cref="ContentModerationHelpers"/>).</summary>
    public const string InstructionLikeFlag = "instruction_like_text";

    private static readonly string[] Patterns =
    {
        "ignore previous",
        "ignore all",
        "ignore the above",
        "disregard",
        "system prompt",
        "developer mode",
        "admin mode",
        "override instructions",
        "new instructions",
        "you are now",
        "</system>",
        "```system",
        "jailbreak",
        "dan mode",
    };

    /// <summary>Scans stored title, body, and optional media URL (defense in depth for query stuffing).</summary>
    public static bool IsInstructionLike(string? title, string? body, string? mediaUrl)
    {
        var blob = string.Join(
            "\n",
            title ?? string.Empty,
            body ?? string.Empty,
            mediaUrl ?? string.Empty).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(blob))
            return false;

        foreach (var p in Patterns)
        {
            if (blob.Contains(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
