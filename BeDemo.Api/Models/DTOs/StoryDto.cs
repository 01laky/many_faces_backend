namespace BeDemo.Api.Models.DTOs;

/// <summary>Slim story row returned in the creator's own story list (ListMine).</summary>
public sealed class StoryListItemDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string State { get; init; } = string.Empty;
	public DateTime? PublishedAt { get; init; }
	public DateTime? ExpiresAt { get; init; }
	public DateTime? ScheduledPublishAt { get; init; }
	public DateTime CreatedAt { get; init; }
	public int ImageCount { get; init; }
	public IEnumerable<int> FaceIds { get; init; } = [];
}

/// <summary>Full story detail returned by GET /api/stories/{id}.</summary>
public sealed class StoryDetailDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? State { get; init; }
	public string? CreatorId { get; init; }
	public string CreatorName { get; init; } = string.Empty;
	public IEnumerable<StoryFaceRefDto> Faces { get; init; } = [];
	public IEnumerable<StoryImageDetailDto> Images { get; init; } = [];
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public DateTime? PublishedAt { get; init; }
	public DateTime? ExpiresAt { get; init; }
	public DateTime? ScheduledPublishAt { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
	public int ViewCount { get; init; }
	public IEnumerable<StoryViewerDto>? Viewers { get; init; }
}

/// <summary>Face reference in a story detail response.</summary>
public sealed class StoryFaceRefDto
{
	public int FaceId { get; init; }
	public string Title { get; init; } = string.Empty;
}

/// <summary>Image within a story detail response.</summary>
public sealed class StoryImageDetailDto
{
	public int Id { get; init; }
	public string ImageUrl { get; init; } = string.Empty;
	public string? Description { get; init; }
	public int SortOrder { get; init; }
}

/// <summary>Viewer entry within a story detail response (operator/creator only).</summary>
public sealed class StoryViewerDto
{
	public string? ViewerUserId { get; init; }
	public string ViewerName { get; init; } = string.Empty;
	public DateTime ViewedAt { get; init; }
}

/// <summary>Slim story create response returned by POST /api/stories.</summary>
public sealed class StoryCreatedDto
{
	public int Id { get; init; }
}

/// <summary>Story image upload response returned by POST /api/stories/{id}/images.</summary>
public sealed class StoryImageUploadResultDto
{
	public int Id { get; init; }
	public string ImageUrl { get; init; } = string.Empty;
	public int SortOrder { get; init; }
}
