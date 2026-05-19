namespace BeDemo.Api.Models.Requests.Users;

public sealed class GetUsersQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public bool ForAddFriend { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class DeletePushTokenQuery
{
    public string? InstallationId { get; set; }
}
