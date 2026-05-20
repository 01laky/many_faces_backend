namespace BeDemo.Api.Models.Requests.Faces;

public sealed class FaceProfileListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class FaceProfileCommentsListQuery
{
    /// <summary>0 = portal legacy array; &gt;= 1 = operator paginated envelope.</summary>
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class FaceProfileReviewsListQuery
{
    /// <summary>0 = portal legacy array; &gt;= 1 = operator paginated envelope.</summary>
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class FaceChatRoomListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public bool? IsPublic { get; set; }
}

public sealed class ChatMessagesQuery
{
    /// <summary>0 = cursor mode (portal <c>beforeId</c>); &gt;= 1 = offset envelope for operator admin tables.</summary>
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
    public int? BeforeId { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class FaceChatRoomMembersListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class FaceChatRoomJoinRequestsListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class WallTicketListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class WallTicketWriteDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class WallTicketCommentDto
{
    public string Content { get; set; } = string.Empty;
}
