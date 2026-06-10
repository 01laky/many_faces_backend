using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Full album detail returned by GET /api/albums/{id} and PUT /api/albums/{id}.</summary>
public sealed class AlbumDetailDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public int AlbumType { get; init; }
	public int MediaType { get; init; }
	public string? CreatorId { get; init; }
	public string CreatorName { get; init; } = string.Empty;
	public IEnumerable<AlbumFaceDto> Faces { get; init; } = [];
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? AiReviewUserMessage { get; init; }
	public string? HumanDecisionReason { get; init; }
	public DateTime? SubmittedAtUtc { get; init; }
	public string? CreatorStatusLabel { get; init; }
	public int MediaCount { get; init; }
	public IEnumerable<AlbumMediaItemDto> MediaItems { get; init; } = [];
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }

	public static AlbumDetailDto From(Album album, string currentUserId, bool showModerationFields) => new()
	{
		Id = album.Id,
		Title = album.Title,
		Description = album.Description,
		AlbumType = (int)album.AlbumType,
		MediaType = (int)album.MediaType,
		CreatorId = album.CreatorId,
		CreatorName = ((album.Creator?.FirstName ?? "") + " " + (album.Creator?.LastName ?? "")).Trim(),
		Faces = album.AlbumFaces.Select(af => new AlbumFaceDto { FaceId = af.FaceId, Title = af.Face?.Title ?? string.Empty }),
		LikesCount = album.Likes.Count,
		CommentsCount = album.Comments.Count,
		IsLikedByMe = album.Likes.Any(l => l.UserId == currentUserId),
		ApprovalStatus = album.ApprovalStatus.ToString(),
		AiReviewStatus = album.AiReviewStatus.ToString(),
		AiReviewUserMessage = showModerationFields ? album.AiReviewUserMessage : null,
		HumanDecisionReason = showModerationFields ? album.HumanDecisionReason : null,
		SubmittedAtUtc = showModerationFields ? album.SubmittedAtUtc : null,
		CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(album.ApprovalStatus, album.AiReviewStatus),
		MediaCount = album.MediaItems.Count,
		MediaItems = album.MediaItems
			.OrderBy(m => m.SortOrder)
			.Select(m => new AlbumMediaItemDto
			{
				Id = m.Id,
				MediaType = m.MediaType.ToString(),
				ImageUrl = m.ImageUrl,
				VideoUrl = m.VideoUrl,
				ThumbnailUrl = m.ThumbnailUrl,
				SortOrder = m.SortOrder,
				Title = m.Title,
			}),
		CreatedAt = album.CreatedAt,
		UpdatedAt = album.UpdatedAt,
	};
}

/// <summary>Slim album response returned by POST /api/albums (create) and some update paths.</summary>
public sealed class AlbumCreateResultDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public int AlbumType { get; init; }
	public int MediaType { get; init; }
	public string? CreatorId { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? CreatorStatusLabel { get; init; }
	public DateTime CreatedAt { get; init; }

	public static AlbumCreateResultDto From(Album album) => new()
	{
		Id = album.Id,
		Title = album.Title,
		Description = album.Description,
		AlbumType = (int)album.AlbumType,
		MediaType = (int)album.MediaType,
		CreatorId = album.CreatorId,
		ApprovalStatus = album.ApprovalStatus.ToString(),
		AiReviewStatus = album.AiReviewStatus.ToString(),
		CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(album.ApprovalStatus, album.AiReviewStatus),
		CreatedAt = album.CreatedAt,
	};
}

/// <summary>Face reference within an album detail response.</summary>
public sealed class AlbumFaceDto
{
	public int FaceId { get; init; }
	public string Title { get; init; } = string.Empty;
}

/// <summary>Media item within an album detail response.</summary>
public sealed class AlbumMediaItemDto
{
	public int Id { get; init; }
	public string MediaType { get; init; } = string.Empty;
	public string? ImageUrl { get; init; }
	public string? VideoUrl { get; init; }
	public string? ThumbnailUrl { get; init; }
	public int SortOrder { get; init; }
	public string? Title { get; init; }
}

/// <summary>Slim album row returned by GET /api/albums/user/{userId}.</summary>
public sealed class AlbumListItemDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public int AlbumType { get; init; }
	public int MediaType { get; init; }
	public string? CreatorId { get; init; }
	public string CreatorName { get; init; } = string.Empty;
	public IEnumerable<AlbumFaceDto> Faces { get; init; } = [];
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public string ApprovalStatus { get; init; } = string.Empty;
	public string AiReviewStatus { get; init; } = string.Empty;
	public string? CreatorStatusLabel { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}
