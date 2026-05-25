using BeDemo.Api.Data;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services.Search;

/// <summary>Defense-in-depth ACL filter: drop ES hits that fail §3.4 visibility in PostgreSQL.</summary>
public sealed class SearchHitAclFilter
{
    private readonly ApplicationDbContext _db;

    public SearchHitAclFilter(ApplicationDbContext db) => _db = db;

    public async Task<bool> IsVisibleAsync(AutocompleteHit hit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hit.DocumentType) || string.IsNullOrWhiteSpace(hit.EntityId))
            return false;

        return hit.DocumentType switch
        {
            SearchDocumentTypes.User => await IsUserVisibleAsync(hit.EntityId, cancellationToken),
            SearchDocumentTypes.Face => await _db.Faces.AsNoTracking().AnyAsync(f => f.Id.ToString() == hit.EntityId, cancellationToken),
            SearchDocumentTypes.Page => await _db.Pages.AsNoTracking().AnyAsync(p => p.Id.ToString() == hit.EntityId, cancellationToken),
            SearchDocumentTypes.Album => await _db.Albums.AsNoTracking()
                .AnyAsync(a => a.Id.ToString() == hit.EntityId && a.RemovedAtUtc == null, cancellationToken),
            SearchDocumentTypes.Blog => await _db.Blogs.AsNoTracking()
                .AnyAsync(b => b.Id.ToString() == hit.EntityId && b.RemovedAtUtc == null, cancellationToken),
            SearchDocumentTypes.Reel => await _db.Reels.AsNoTracking()
                .AnyAsync(r => r.Id.ToString() == hit.EntityId && r.RemovedAtUtc == null, cancellationToken),
            SearchDocumentTypes.Story => await _db.Stories.AsNoTracking()
                .AnyAsync(s => s.Id.ToString() == hit.EntityId, cancellationToken),
            SearchDocumentTypes.FaceChatRoom => await _db.FaceChatRooms.AsNoTracking()
                .AnyAsync(r => r.Id.ToString() == hit.EntityId, cancellationToken),
            SearchDocumentTypes.VideoLounge => await _db.FaceVideoLounges.AsNoTracking()
                .AnyAsync(l => l.Id.ToString() == hit.EntityId, cancellationToken),
            SearchDocumentTypes.FaceProfile => await _db.UserFaceProfiles.AsNoTracking()
                .AnyAsync(p => p.Id.ToString() == hit.EntityId && p.IsActive, cancellationToken),
            SearchDocumentTypes.WallTicket => await _db.FaceWallTickets.AsNoTracking()
                .AnyAsync(t => t.Id.ToString() == hit.EntityId, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> IsUserVisibleAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.LockoutEnd })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return false;

        return SearchIndexVisibility.IsUserIndexable(
            new Models.ApplicationUser { Id = userId, Email = user.Email },
            user.LockoutEnd);
    }
}
