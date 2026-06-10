namespace BeDemo.Api.Models.DTOs;

/// <summary>Blog comment create/update response.</summary>
public sealed class BlogCommentDto
{
	public int Id { get; init; }
	public int BlogId { get; init; }
	public string? UserId { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Album comment create/update response.</summary>
public sealed class AlbumCommentDto
{
	public int Id { get; init; }
	public int AlbumId { get; init; }
	public string? UserId { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Reel comment create/update response.</summary>
public sealed class ReelCommentDto
{
	public int Id { get; init; }
	public int ReelId { get; init; }
	public string? UserId { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Album comment list item returned by GET /api/albums/{id}/comments (includes display name).</summary>
public sealed class AlbumCommentListItemDto
{
	public int Id { get; init; }
	public int AlbumId { get; init; }
	public string? UserId { get; init; }
	public string UserName { get; init; } = string.Empty;
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Blog comment list item returned by GET /api/blogs/{id}/comments (includes display name).</summary>
public sealed class BlogCommentListItemDto
{
	public int Id { get; init; }
	public int BlogId { get; init; }
	public string? UserId { get; init; }
	public string UserName { get; init; } = string.Empty;
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Reel comment list item returned by GET /api/reels/{id}/comments (includes display name).</summary>
public sealed class ReelCommentListItemDto
{
	public int Id { get; init; }
	public int ReelId { get; init; }
	public string? UserId { get; init; }
	public string UserName { get; init; } = string.Empty;
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Story comment list item returned by GET /api/stories/{id}/comments (includes display name).</summary>
public sealed class StoryCommentListItemDto
{
	public int Id { get; init; }
	public string? UserId { get; init; }
	public string UserName { get; init; } = string.Empty;
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
}
