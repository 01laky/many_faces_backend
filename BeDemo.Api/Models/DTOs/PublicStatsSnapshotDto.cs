namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Aggregate counts only (no per-user rows, no OAuth secrets, no moderation audit detail).
/// Intended for anonymous <c>GET /api/Stats/public</c> and for AI prompts where only non-identifying totals are needed.
/// </summary>
public sealed class PublicStatsSnapshotDto
{
    public int UsersCount { get; init; }
    public int FacesCount { get; init; }
    public int PagesCount { get; init; }
    public int FriendshipsCount { get; init; }
    public int FriendRequestsPendingCount { get; init; }
    public int MessagesCount { get; init; }
    public int AlbumsCount { get; init; }
    public int BlogsCount { get; init; }
    public int ReelsCount { get; init; }
    public int StoriesCount { get; init; }
    public int StoryViewsCount { get; init; }
    public int FaceWallTicketsCount { get; init; }
    public int FaceChatRoomsCount { get; init; }
    public int FaceChatRoomMessagesCount { get; init; }
}
