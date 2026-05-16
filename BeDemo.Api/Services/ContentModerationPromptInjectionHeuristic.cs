namespace BeDemo.Api.Services;

/// <summary>
/// Lightweight, high-recall / low-precision detector for instruction-like phrases in user submissions.
/// A positive match must never auto-approve: <see cref="ContentModerationHelpers.ValidateRecommendation"/> forces human review when combined with <see cref="AiReviewDecision.Approve"/>.
/// </summary>
public static class ContentModerationPromptInjectionHeuristic
{
    /// <summary>Stored on <c>AiReviewFlagsJson</c> when the heuristic fires (also whitelisted in <see cref="ContentModerationHelpers"/>).</summary>
    public const string InstructionLikeFlag = "instruction_like_text";

    /// <summary>
    /// Lower-case substrings observed in real prompt-injection attempts (English + SK/CZ samples + delimiter smuggling).
    /// High recall is intentional; false positives route to human review, never to auto-approve.
    /// </summary>
    private static readonly string[] Patterns =
    {
        "ignore previous",
        "ignore all",
        "ignore the above",
        "ignoruj predch",
        "ignoruj předch",
        "disregard",
        "forget your",
        "system prompt",
        "developer mode",
        "admin mode",
        "override instructions",
        "new instructions",
        "prior rules",
        "you are now",
        "pretend you",
        "act as",
        "output your",
        "no restrictions",
        "</system>",
        "```system",
        "<|im_start|>",
        "jailbreak",
        "dan mode",
        "bypass",
        "hypothetically",
        "regardless of policy",
        "regardless of",
        "allow all content",
        "system:",
        "#system",
        "override safety",
        "approve this post",
        "recommendedapprove",
    };

    /// <summary>
    /// Scans stored title, body, and optional media URL (defense in depth for query-string stuffing).
    /// Uses <see cref="ContentModerationTextNormalization.BuildHeuristicScanBlob"/> so zero-width / bidi smuggling cannot hide phrases.
    /// </summary>
    public static bool IsInstructionLike(string? title, string? body, string? mediaUrl)
    {
        var blob = ContentModerationTextNormalization.BuildHeuristicScanBlob(title, body, mediaUrl);
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
