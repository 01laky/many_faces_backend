namespace BeDemo.Api.Models.DTOs;

/// <summary>Single wall ticket item in the paginated list.</summary>
public sealed class WallTicketListItemDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string DescriptionPreview { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public string CreatorId { get; init; } = string.Empty;
	public string CreatorName { get; init; } = string.Empty;
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public bool IsAuthor { get; init; }
	public DateTime CreatedAt { get; init; }
	public bool CanInteract { get; init; }
	public bool IsHostViewer { get; init; }
}

/// <summary>Paginated wall ticket list envelope.</summary>
public sealed class WallTicketListEnvelopeDto
{
	public IEnumerable<WallTicketListItemDto> Items { get; init; } = [];
	public bool IsHostViewer { get; init; }
	public int Page { get; init; }
	public int PageSize { get; init; }
	public int TotalCount { get; init; }
	public int TotalPages { get; init; }
}

/// <summary>Single comment item inside a wall ticket detail response.</summary>
public sealed class WallTicketDetailCommentDto
{
	public int Id { get; init; }
	public string? Content { get; init; }
	public string UserId { get; init; } = string.Empty;
	public string AuthorName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Full wall ticket detail (portal and admin views).</summary>
public sealed class WallTicketDetailDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string Status { get; init; } = string.Empty;
	public string CreatorId { get; init; } = string.Empty;
	public string CreatorName { get; init; } = string.Empty;
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public bool IsLikedByMe { get; init; }
	public bool IsAuthor { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
	public bool CanInteract { get; init; }
	public bool InteractionsFrozen { get; init; }
	public bool IsHostViewer { get; init; }
	public IEnumerable<WallTicketDetailCommentDto> Comments { get; init; } = [];
}

/// <summary>Wall ticket create response (slim).</summary>
public sealed class WallTicketCreatedDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Wall ticket update response.</summary>
public sealed class WallTicketUpdateResultDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Wall ticket comment create response.</summary>
public sealed class WallTicketCommentResultDto
{
	public int Id { get; init; }
	public string? Content { get; init; }
	public string? UserId { get; init; }
	public string AuthorName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}
