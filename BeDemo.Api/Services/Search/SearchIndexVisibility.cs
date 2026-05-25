using BeDemo.Api.Models;

namespace BeDemo.Api.Services.Search;

/// <summary>
/// Single source of truth for which PostgreSQL rows may appear in the admin search index (§3.4 / §6.4).
/// Outbox enqueue, reconciliation scans, and orphan PG id sets must all call these helpers.
/// </summary>
public static class SearchIndexVisibility
{
    /// <summary>Active platform users operators can open in UserDetailPage — excludes globally banned accounts.</summary>
    public static bool IsUserIndexable(ApplicationUser user, DateTimeOffset? lockoutEnd)
    {
        if (user is null)
            return false;

        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow.AddYears(50))
            return false;

        return !string.IsNullOrWhiteSpace(user.Email);
    }

    /// <summary>Tenant faces visible to super-admin (row existence is sufficient; hard deletes remove the row).</summary>
    public static bool IsFaceIndexable(Face face) => face is not null;

    /// <summary>CMS pages super-admin can open (published or draft).</summary>
    public static bool IsPageIndexable(Page page) => page is not null;

    /// <summary>Moderation-visible albums — exclude soft-removed rows only.</summary>
    public static bool IsAlbumIndexable(Album album) => album is not null && album.RemovedAtUtc is null;

    public static bool IsBlogIndexable(Blog blog) => blog is not null && blog.RemovedAtUtc is null;

    public static bool IsReelIndexable(Reel reel) => reel is not null && reel.RemovedAtUtc is null;

    /// <summary>Stories have no RemovedAtUtc; any existing row is findable in admin moderation.</summary>
    public static bool IsStoryIndexable(Story story) => story is not null;

    public static bool IsFaceChatRoomIndexable(FaceChatRoom room) => room is not null;

    public static bool IsVideoLoungeIndexable(FaceVideoLounge lounge) => lounge is not null;

    /// <summary>Profiles linked to active face membership.</summary>
    public static bool IsFaceProfileIndexable(UserFaceProfile profile) =>
        profile is not null && profile.IsActive;

    /// <summary>Open and closed wall tickets visible on FaceWallTicketsPage.</summary>
    public static bool IsWallTicketIndexable(FaceWallTicket ticket) => ticket is not null;
}
