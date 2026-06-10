using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Moderation;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <summary>Maps moderated entities to unified queue DTOs for <c>ContentModerationController</c>.</summary>
public static class ContentModerationQueueMapper
{
	public static ModerationItemDto MapAlbum(Album album, int faceId, string faceTitle) =>
		new(
			ModeratedContentType.Album,
			album.Id,
			album.Title,
			faceId,
			faceTitle,
			album.CreatorId,
			CreatorDisplayName(album.Creator),
			album.ApprovalStatus,
			album.AiReviewStatus,
			album.AiReviewDecision,
			album.AiReviewConfidence,
			album.AiReviewRiskLevel,
			album.AiReviewFlagsJson,
			album.AiReviewReason,
			album.AiReviewUserMessage,
			album.AiReviewModelVersion,
			album.AiReviewTraceId,
			album.SubmittedAtUtc,
			album.HumanReviewedAtUtc,
			album.HumanDecisionReason,
			album.RemovedAtUtc,
			album.RemovalReason,
			album.CreatedAt,
			ContentModerationPreviewText.ToPlainTextPreview(album.Description),
			null);

	public static ModerationItemDto MapBlog(Blog blog, string faceTitle, string? firstImageUrl) =>
		new(
			ModeratedContentType.Blog,
			blog.Id,
			blog.Title,
			blog.FaceId,
			faceTitle,
			blog.CreatorId,
			CreatorDisplayName(blog.Creator),
			blog.ApprovalStatus,
			blog.AiReviewStatus,
			blog.AiReviewDecision,
			blog.AiReviewConfidence,
			blog.AiReviewRiskLevel,
			blog.AiReviewFlagsJson,
			blog.AiReviewReason,
			blog.AiReviewUserMessage,
			blog.AiReviewModelVersion,
			blog.AiReviewTraceId,
			blog.SubmittedAtUtc,
			blog.HumanReviewedAtUtc,
			blog.HumanDecisionReason,
			blog.RemovedAtUtc,
			blog.RemovalReason,
			blog.CreatedAt,
			ContentModerationPreviewText.ToPlainTextPreview(blog.Content),
			ContentModerationPreviewText.ToMediaUrlPreview(firstImageUrl));

	public static ModerationItemDto MapReel(Reel reel, int faceId, string faceTitle) =>
		new(
			ModeratedContentType.Reel,
			reel.Id,
			reel.Title,
			faceId,
			faceTitle,
			reel.CreatorId,
			CreatorDisplayName(reel.Creator),
			reel.ApprovalStatus,
			reel.AiReviewStatus,
			reel.AiReviewDecision,
			reel.AiReviewConfidence,
			reel.AiReviewRiskLevel,
			reel.AiReviewFlagsJson,
			reel.AiReviewReason,
			reel.AiReviewUserMessage,
			reel.AiReviewModelVersion,
			reel.AiReviewTraceId,
			reel.SubmittedAtUtc,
			reel.HumanReviewedAtUtc,
			reel.HumanDecisionReason,
			reel.RemovedAtUtc,
			reel.RemovalReason,
			reel.CreatedAt,
			ContentModerationPreviewText.ToPlainTextPreview(reel.Description),
			ContentModerationPreviewText.ToMediaUrlPreview(reel.VideoUrl));

	public static string CreatorDisplayName(ApplicationUser creator) =>
		$"{creator.FirstName ?? ""} {creator.LastName ?? ""}".Trim();
}
