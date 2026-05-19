namespace BeDemo.Api.Models.Requests.Faces;

public sealed class FaceProfileListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ChatMessagesQuery
{
    public int PageSize { get; set; } = 50;
    public int? BeforeId { get; set; }
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
