using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Full reel detail returned by GET /api/reels/{id}, POST /api/reels, and PUT /api/reels/{id}.</summary>
public sealed class ReelDetailDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? VideoUrl { get; init; }
	public string? CreatorId { get; init; }
	public string CreatorName { get; init; } = string.Empty;
	public IEnumerable<ReelFaceDto> Faces { get; init; } = [];
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? AiReviewUserMessage { get; init; }
	public string? HumanDecisionReason { get; init; }
	public DateTime? SubmittedAtUtc { get; init; }
	public string? AiReviewDecision { get; init; }
	public string? AiReviewRiskLevel { get; init; }
	public string? AiReviewFlagsJson { get; init; }
	public string? AiReviewReason { get; init; }
	public string? AiReviewModelVersion { get; init; }
	public string? AiReviewTraceId { get; init; }
	public string? CreatorStatusLabel { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }

	public static ReelDetailDto From(Reel reel, string currentUserId, bool showModerationFields) => new()
	{
		Id = reel.Id,
		Title = reel.Title,
		Description = reel.Description,
		VideoUrl = reel.VideoUrl,
		CreatorId = reel.CreatorId,
		CreatorName = ((reel.Creator?.FirstName ?? "") + " " + (reel.Creator?.LastName ?? "")).Trim(),
		Faces = reel.ReelFaces.Select(rf => new ReelFaceDto { FaceId = rf.FaceId, Title = rf.Face?.Title ?? string.Empty }),
		LikesCount = reel.Likes.Count,
		CommentsCount = reel.Comments.Count,
		IsLikedByMe = reel.Likes.Any(l => l.UserId == currentUserId),
		ApprovalStatus = reel.ApprovalStatus.ToString(),
		AiReviewStatus = reel.AiReviewStatus.ToString(),
		AiReviewUserMessage = showModerationFields ? reel.AiReviewUserMessage : null,
		HumanDecisionReason = showModerationFields ? reel.HumanDecisionReason : null,
		SubmittedAtUtc = showModerationFields ? reel.SubmittedAtUtc : null,
		AiReviewDecision = showModerationFields ? reel.AiReviewDecision.ToString() : null,
		AiReviewRiskLevel = showModerationFields ? reel.AiReviewRiskLevel.ToString() : null,
		AiReviewFlagsJson = showModerationFields ? reel.AiReviewFlagsJson : null,
		AiReviewReason = showModerationFields ? reel.AiReviewReason : null,
		AiReviewModelVersion = showModerationFields ? reel.AiReviewModelVersion : null,
		AiReviewTraceId = showModerationFields ? reel.AiReviewTraceId : null,
		CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(reel.ApprovalStatus, reel.AiReviewStatus),
		CreatedAt = reel.CreatedAt,
		UpdatedAt = reel.UpdatedAt,
	};
}

/// <summary>Face reference within a reel detail response.</summary>
public sealed class ReelFaceDto
{
	public int FaceId { get; init; }
	public string Title { get; init; } = string.Empty;
}
