using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Full blog detail returned by GET /api/blogs/{id} and PUT /api/blogs/{id}.</summary>
public sealed class BlogDetailDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Content { get; init; }
	public string? ContentPlainText { get; init; }
	public int FaceId { get; init; }
	public string FaceTitle { get; init; } = string.Empty;
	public string? CreatorId { get; init; }
	public string CreatorName { get; init; } = string.Empty;
	public IEnumerable<BlogImageDto> Images { get; init; } = [];
	public int ImageCount { get; init; }
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? AiReviewUserMessage { get; init; }
	public string? HumanDecisionReason { get; init; }
	public DateTime? SubmittedAtUtc { get; init; }
	public DateTime? RemovedAtUtc { get; init; }
	public string? RemovalReason { get; init; }
	public string? AiReviewDecision { get; init; }
	public string? AiReviewRiskLevel { get; init; }
	public string? AiReviewFlagsJson { get; init; }
	public string? AiReviewReason { get; init; }
	public string? AiReviewModelVersion { get; init; }
	public string? AiReviewTraceId { get; init; }
	public double? AiReviewConfidence { get; init; }
	public string? CreatorStatusLabel { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }

	public static BlogDetailDto From(Blog blog, string currentUserId, bool showModerationFields) => new()
	{
		Id = blog.Id,
		Title = blog.Title,
		Content = blog.Content,
		ContentPlainText = ContentModerationPreviewText.ToPlainTextPreview(blog.Content),
		FaceId = blog.FaceId,
		FaceTitle = blog.Face?.Title ?? string.Empty,
		CreatorId = blog.CreatorId,
		CreatorName = ((blog.Creator?.FirstName ?? "") + " " + (blog.Creator?.LastName ?? "")).Trim(),
		Images = blog.Images.OrderBy(i => i.SortOrder).Select(i => new BlogImageDto { Id = i.Id, ImageUrl = i.ImageUrl, SortOrder = i.SortOrder }),
		ImageCount = blog.Images.Count,
		LikesCount = blog.Likes.Count,
		CommentsCount = blog.Comments.Count,
		IsLikedByMe = blog.Likes.Any(l => l.UserId == currentUserId),
		ApprovalStatus = blog.ApprovalStatus.ToString(),
		AiReviewStatus = blog.AiReviewStatus.ToString(),
		AiReviewUserMessage = showModerationFields ? blog.AiReviewUserMessage : null,
		HumanDecisionReason = showModerationFields ? blog.HumanDecisionReason : null,
		SubmittedAtUtc = showModerationFields ? blog.SubmittedAtUtc : null,
		RemovedAtUtc = showModerationFields ? blog.RemovedAtUtc : null,
		RemovalReason = showModerationFields ? blog.RemovalReason : null,
		AiReviewDecision = showModerationFields ? blog.AiReviewDecision.ToString() : null,
		AiReviewRiskLevel = showModerationFields ? blog.AiReviewRiskLevel.ToString() : null,
		AiReviewFlagsJson = showModerationFields ? blog.AiReviewFlagsJson : null,
		AiReviewReason = showModerationFields ? blog.AiReviewReason : null,
		AiReviewModelVersion = showModerationFields ? blog.AiReviewModelVersion : null,
		AiReviewTraceId = showModerationFields ? blog.AiReviewTraceId : null,
		AiReviewConfidence = showModerationFields ? blog.AiReviewConfidence : null,
		CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(blog.ApprovalStatus, blog.AiReviewStatus),
		CreatedAt = blog.CreatedAt,
		UpdatedAt = blog.UpdatedAt,
	};
}

/// <summary>Slim blog response returned by POST/PUT /api/blogs.</summary>
public sealed class BlogCreateResultDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Content { get; init; }
	public int FaceId { get; init; }
	public string? CreatorId { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? CreatorStatusLabel { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }

	public static BlogCreateResultDto From(Blog blog) => new()
	{
		Id = blog.Id,
		Title = blog.Title,
		Content = blog.Content,
		FaceId = blog.FaceId,
		CreatorId = blog.CreatorId,
		ApprovalStatus = blog.ApprovalStatus.ToString(),
		AiReviewStatus = blog.AiReviewStatus.ToString(),
		CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(blog.ApprovalStatus, blog.AiReviewStatus),
		CreatedAt = blog.CreatedAt,
		UpdatedAt = blog.UpdatedAt,
	};
}

/// <summary>Image within a blog detail response.</summary>
public sealed class BlogImageDto
{
	public int Id { get; init; }
	public string? ImageUrl { get; init; }
	public int SortOrder { get; init; }
}
