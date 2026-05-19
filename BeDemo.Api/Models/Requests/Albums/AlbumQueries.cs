namespace BeDemo.Api.Models.Requests.Albums;

public sealed class AlbumListQuery
{
    public int? FaceId { get; set; }
    /// <summary>When set (operator), list albums created by this user; <see cref="FaceId"/> is optional.</summary>
    public string? CreatorId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? MediaType { get; set; }
    public string? AlbumType { get; set; }
}

public sealed class AlbumByUserQuery
{
    public int? FaceId { get; set; }
}
