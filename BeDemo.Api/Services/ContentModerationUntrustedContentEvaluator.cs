using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Pure evaluation of the untrusted-content defenses applied in <see cref="ContentAiReviewService.ProcessQueuedReviewAsync"/>
/// (sanitize → gRPC payload, heuristic on stored fields, flag merge, <see cref="ContentModerationHelpers.ValidateRecommendation"/>).
/// </summary>
/// <remarks>
/// Used by red-team corpus tests to assert every attack line blocks an unsafe <see cref="AiReviewStatus.RecommendedApprove"/>
/// outcome when the AI returns a high-confidence approve recommendation.
/// </remarks>
public static class ContentModerationUntrustedContentEvaluator
{
    /// <summary>
    /// Replays post-AI merge logic from <see cref="ContentAiReviewService"/> without database or gRPC side effects.
    /// </summary>
    /// <param name="storedTitle">Title as persisted on the entity (heuristic scans stored bytes, not sanitized wire form).</param>
    /// <param name="storedBody">Body/description as persisted.</param>
    /// <param name="storedMediaUrl">Optional media URL as persisted (reels, blog images).</param>
    /// <param name="aiRecommendation">Raw recommendation returned by <c>many_faces_ai</c> <c>ReviewContent</c>.</param>
    /// <param name="instructionHeuristicEnabled">Mirrors <see cref="ContentModerationSecurityOptions.InstructionHeuristicEnabled"/>.</param>
    public static UntrustedContentPipelineResult EvaluateAfterAiRecommendation(
        string? storedTitle,
        string? storedBody,
        string? storedMediaUrl,
        AiReviewRecommendation aiRecommendation,
        bool instructionHeuristicEnabled = true)
    {
        var instructionMatched = instructionHeuristicEnabled &&
            ContentModerationPromptInjectionHeuristic.IsInstructionLike(
                storedTitle,
                storedBody,
                storedMediaUrl);

        var merged = aiRecommendation;
        if (instructionMatched)
        {
            var flags = merged.Flags.ToList();
            var needle = ContentModerationPromptInjectionHeuristic.InstructionLikeFlag;
            if (!flags.Exists(f => string.Equals(f, needle, StringComparison.OrdinalIgnoreCase)))
                flags.Add(needle);
            merged = merged with { Flags = flags };
        }

        merged = merged with { Flags = ContentModerationHelpers.NormalizeAiFlags(merged.Flags) };
        var validation = ContentModerationHelpers.ValidateRecommendation(merged);

        var wouldBeStatus = validation.IsValid
            ? merged.Decision switch
            {
                AiReviewDecision.Approve => AiReviewStatus.RecommendedApprove,
                AiReviewDecision.Reject => AiReviewStatus.RecommendedReject,
                _ => AiReviewStatus.NeedsHumanReview,
            }
            : AiReviewStatus.NeedsHumanReview;

        return new UntrustedContentPipelineResult(
            instructionMatched,
            validation,
            wouldBeStatus,
            AllowsRecommendedApprove: wouldBeStatus == AiReviewStatus.RecommendedApprove);
    }

    /// <summary>
    /// Sanitized wire payload that would be sent to gRPC after <see cref="ContentModerationInputSanitizer.SanitizeForAiReview"/>.
    /// </summary>
    public static (string Title, string Body, string? MediaUrl) SanitizedWireFields(
        string? storedTitle,
        string? storedBody,
        string? storedMediaUrl) =>
        ContentModerationInputSanitizer.SanitizeForAiReview(storedTitle, storedBody, storedMediaUrl);
}

/// <summary>Outcome of <see cref="ContentModerationUntrustedContentEvaluator.EvaluateAfterAiRecommendation"/>.</summary>
/// <param name="InstructionHeuristicMatched">True when instruction-like patterns were detected on stored fields.</param>
/// <param name="Validation">Backend policy validation applied to the merged AI recommendation.</param>
/// <param name="WouldBeAiReviewStatus">Resulting <see cref="AiReviewStatus"/> if <see cref="ContentAiReviewService.ApplyRecommendation"/> ran.</param>
/// <param name="AllowsRecommendedApprove">True only when the pipeline would surface a high-confidence AI approve to moderators.</param>
public sealed record UntrustedContentPipelineResult(
    bool InstructionHeuristicMatched,
    AiRecommendationValidationResult Validation,
    AiReviewStatus WouldBeAiReviewStatus,
    bool AllowsRecommendedApprove);
