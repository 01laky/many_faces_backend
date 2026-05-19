namespace BeDemo.Api.Models.DTOs;

public sealed class AdminInviteListResponseDto
{
    public IReadOnlyList<RegistrationInviteListItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
