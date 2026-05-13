using System.Text.Json;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Shared, side-effect-free helpers for the user-generated content moderation pipeline:
/// policy checks, AI payload serialization, audit redaction, and retention helpers.
/// </summary>
public static class ContentModerationHelpers
{
    /// <summary>Maximum AI attempts per moderation version before forcing human review.</summary>
    public const int DefaultMaxAttempts = 3;
    /// <summary>Guards against duplicate concurrent jobs for the same content version.</summary>
    public const int DefaultPerContentQueueLimit = 1;
    /// <summary>Upper bound for bulk moderation batch sizes (controller may enforce a lower cap).</summary>
    public const int DefaultBatchSizeLimit = 25;
    /// <summary>Redis job type consumed by the background worker to call the AI gRPC service.</summary>
    public const string AiReviewJobType = "content.ai-review";
    /// <summary>Days after terminal moderation before internal AI fields may be redacted by retention.</summary>
    public const int DefaultRetentionDays = 180;

    /// <summary>Human-readable status for creator dashboards (matches FE grouping expectations).</summary>
    public static string CreatorStatusLabel(ContentApprovalStatus approvalStatus, AiReviewStatus aiReviewStatus) =>
        approvalStatus switch
        {
            ContentApprovalStatus.PendingApproval when aiReviewStatus == AiReviewStatus.InProgress => "Under AI review",
            ContentApprovalStatus.PendingApproval when aiReviewStatus == AiReviewStatus.NeedsHumanReview => "Needs review",
            ContentApprovalStatus.PendingApproval => "Pending approval",
            ContentApprovalStatus.Approved => "Approved",
            ContentApprovalStatus.Rejected => "Rejected",
            ContentApprovalStatus.Removed => "Removed",
            _ => "Pending approval",
        };

    public static bool IsPubliclyVisible(ContentApprovalStatus approvalStatus) =>
        approvalStatus == ContentApprovalStatus.Approved;

    /// <summary>Creators may edit while moderation is still open or after rejection (resubmit flow).</summary>
    public static bool IsCreatorEditable(ContentApprovalStatus approvalStatus) =>
        approvalStatus is ContentApprovalStatus.PendingApproval or ContentApprovalStatus.Rejected;

    /// <summary>Deletion is restricted to the same states as edit to avoid removing published catalog content.</summary>
    public static bool IsCreatorDeletable(ContentApprovalStatus approvalStatus) =>
        approvalStatus is ContentApprovalStatus.PendingApproval or ContentApprovalStatus.Rejected;

    /// <summary>Validates absolute http(s) URLs used for thumbnails, reels, or embedded media references.</summary>
    public static bool IsSafeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>Checks file extension on the URL path against an allow-list (e.g. .jpg, .mp4).</summary>
    public static bool HasSupportedMediaExtension(string? value, params string[] allowedExtensions)
    {
        if (!IsSafeHttpUrl(value))
            return false;
        if (!Uri.TryCreate(value!.Trim(), UriKind.Absolute, out var uri))
            return false;
        var extension = Path.GetExtension(uri.AbsolutePath);
        return !string.IsNullOrWhiteSpace(extension) &&
            allowedExtensions.Any(allowed => string.Equals(extension, allowed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsRetentionDue(DateTime? statusChangedAtUtc, DateTime nowUtc, int retentionDays = DefaultRetentionDays) =>
        statusChangedAtUtc.HasValue && statusChangedAtUtc.Value <= nowUtc.AddDays(-Math.Max(1, retentionDays));

    /// <summary>Flags emitted by <c>many_faces_ai</c> <c>ReviewContent</c> plus backend-only markers (e.g. instruction-like heuristic). Unknown values are dropped.</summary>
    public static IReadOnlyList<string> NormalizeAiFlags(IReadOnlyList<string>? flags)
    {
        if (flags == null || flags.Count == 0)
            return Array.Empty<string>();

        static string? MatchCanonical(string raw)
        {
            foreach (var c in CanonicalAiFlagNames)
            {
                if (c.Equals(raw, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        var list = new List<string>();
        foreach (var f in flags)
        {
            if (string.IsNullOrWhiteSpace(f))
                continue;
            var canon = MatchCanonical(f.Trim());
            if (canon != null && !list.Contains(canon, StringComparer.Ordinal))
                list.Add(canon);
        }

        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>
    /// Backend guard-rails so invalid or unsafe AI JSON cannot auto-publish content.
    /// Returns <see cref="AiRecommendationValidationResult.NeedsHumanReview"/> when manual review is required.
    /// </summary>
    public static AiRecommendationValidationResult ValidateRecommendation(AiReviewRecommendation recommendation)
    {
        var flags = NormalizeAiFlags(recommendation.Flags);
        var rec = recommendation with { Flags = flags };

        if (!Enum.IsDefined(rec.Decision))
            return AiRecommendationValidationResult.NeedsHumanReview("Unknown AI decision.");
        if (rec.Confidence is < 0 or > 1)
            return AiRecommendationValidationResult.NeedsHumanReview("AI confidence must be between 0 and 1.");
        if (rec.RiskLevel == AiReviewRiskLevel.High && rec.Decision == AiReviewDecision.Approve)
            return AiRecommendationValidationResult.NeedsHumanReview("High-risk content cannot be auto-approved.");
        if (rec.Decision == AiReviewDecision.Reject && string.IsNullOrWhiteSpace(rec.Reason))
            return AiRecommendationValidationResult.NeedsHumanReview("Reject recommendations require a reason.");
        if (rec.Decision == AiReviewDecision.Approve &&
            rec.Flags.Any(f => string.Equals(f, ContentModerationPromptInjectionHeuristic.InstructionLikeFlag, StringComparison.Ordinal)))
            return AiRecommendationValidationResult.NeedsHumanReview(
                "Instruction-like text requires human review before approval.");

        return AiRecommendationValidationResult.Valid();
    }

    /// <summary>Lower-case canonical names aligned with <c>many_faces_ai</c> <c>ReviewContent</c> plus <see cref="ContentModerationPromptInjectionHeuristic.InstructionLikeFlag"/>.</summary>
    private static readonly string[] CanonicalAiFlagNames =
    {
        "spam",
        "scam",
        "phishing",
        "hate",
        "harassment",
        "adult",
        "violence",
        "self_harm",
        "copyright",
        "low_quality",
        "unsafe_link",
        "unsupported_media",
        "image_analysis_boundary",
        "video_analysis_boundary",
        ContentModerationPromptInjectionHeuristic.InstructionLikeFlag,
    };

    /// <summary>Factory for consistent audit rows across albums, blogs, and reels.</summary>
    public static ContentModerationEvent BuildEvent(
        ModeratedContentType contentType,
        int contentId,
        int faceId,
        ContentApprovalStatus? oldApprovalStatus,
        ContentApprovalStatus? newApprovalStatus,
        AiReviewStatus? oldAiReviewStatus,
        AiReviewStatus? newAiReviewStatus,
        ModerationActorType actorType,
        string? actorUserId,
        string? reason,
        string? userMessage,
        string? aiTraceId = null,
        string? aiModelVersion = null) =>
        new()
        {
            ContentType = contentType,
            ContentId = contentId,
            FaceId = faceId,
            OldApprovalStatus = oldApprovalStatus,
            NewApprovalStatus = newApprovalStatus,
            OldAiReviewStatus = oldAiReviewStatus,
            NewAiReviewStatus = newAiReviewStatus,
            ActorType = actorType,
            ActorUserId = actorUserId,
            Reason = RedactForAudit(reason),
            UserMessage = userMessage,
            AiTraceId = aiTraceId,
            AiModelVersion = aiModelVersion,
            CreatedAtUtc = DateTime.UtcNow,
        };

    /// <summary>Minimal JSON envelope stored on Redis for the AI review worker.</summary>
    public static string BuildAiReviewPayload(
        ModeratedContentType contentType,
        int contentId,
        int moderationVersion) =>
        JsonSerializer.Serialize(new
        {
            contentType = contentType.ToString(),
            contentId,
            moderationVersion,
        });

    /// <summary>Truncates very long free-text fields before they are written to immutable audit tables.</summary>
    public static string? RedactForAudit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        return trimmed.Length <= 2000 ? trimmed : string.Concat(trimmed.AsSpan(0, 2000), "...");
    }
}

public sealed record AiReviewRecommendation(
    AiReviewDecision Decision,
    double Confidence,
    AiReviewRiskLevel RiskLevel,
    IReadOnlyList<string> Flags,
    string? Reason,
    string? UserMessage,
    string? ModelVersion,
    string? TraceId);

public sealed record AiRecommendationValidationResult(bool IsValid, string? FallbackReason)
{
    public static AiRecommendationValidationResult Valid() => new(true, null);

    public static AiRecommendationValidationResult NeedsHumanReview(string reason) => new(false, reason);
}
