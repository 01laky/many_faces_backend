using BeDemo.Api.Models;

namespace BeDemo.Api.Models.DTOs.Moderation;

/// <param name="BodyPreviewPlainText">SHV2 PI-8: stripped HTML / plain description for operator preview (never raw HTML).</param>
/// <param name="MediaUrlPreview">Optional reel media URL preview (plain string).</param>
public sealed record ModerationItemDto(
    ModeratedContentType ContentType,
    int ContentId,
    string Title,
    int FaceId,
    string FaceTitle,
    string CreatorId,
    string CreatorName,
    ContentApprovalStatus ApprovalStatus,
    AiReviewStatus AiReviewStatus,
    AiReviewDecision AiReviewDecision,
    double? AiReviewConfidence,
    AiReviewRiskLevel AiReviewRiskLevel,
    string? AiReviewFlagsJson,
    string? AiReviewReason,
    string? AiReviewUserMessage,
    string? AiReviewModelVersion,
    string? AiReviewTraceId,
    DateTime? SubmittedAtUtc,
    DateTime? HumanReviewedAtUtc,
    string? HumanDecisionReason,
    DateTime? RemovedAtUtc,
    string? RemovalReason,
    DateTime CreatedAt,
    string BodyPreviewPlainText,
    string? MediaUrlPreview);
