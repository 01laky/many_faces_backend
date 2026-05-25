using BeDemo.Api.Data;
using BeDemo.Api.Models;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services.Search;

/// <summary>Batch ACL filter for autocomplete hits — one query per entity type instead of N round-trips.</summary>
public sealed class SearchHitBatchFilter
{
    private readonly ApplicationDbContext _db;

    public SearchHitBatchFilter(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<AutocompleteHit>> FilterVisibleAsync(
        IReadOnlyList<AutocompleteHit> hits,
        CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
            return hits;

        var visibleIds = await LoadVisibleEntityIdsAsync(hits, cancellationToken);
        var profileUserIds = await LoadFaceProfileUserIdsAsync(hits, cancellationToken);

        var output = new List<AutocompleteHit>(hits.Count);
        foreach (var hit in hits)
        {
            if (string.IsNullOrWhiteSpace(hit.DocumentType) || string.IsNullOrWhiteSpace(hit.EntityId))
                continue;

            if (!visibleIds.TryGetValue(hit.DocumentType, out var allowed) || !allowed.Contains(hit.EntityId))
                continue;

            if (hit.DocumentType == SearchDocumentTypes.FaceProfile
                && profileUserIds.TryGetValue(hit.EntityId, out var userId))
            {
                hit.RouteParams ??= new RouteParams { Type = hit.DocumentType };
                hit.RouteParams.Ids["userId"] = userId;
                hit.RouteParams.Ids.Remove("profileId");
            }

            output.Add(hit);
        }

        return output;
    }

    private async Task<Dictionary<string, HashSet<string>>> LoadVisibleEntityIdsAsync(
        IReadOnlyList<AutocompleteHit> hits,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var group in hits.GroupBy(h => h.DocumentType, StringComparer.Ordinal))
        {
            var ids = group.Select(h => h.EntityId).Distinct(StringComparer.Ordinal).ToList();
            result[group.Key] = group.Key switch
            {
                SearchDocumentTypes.User => await FilterUserIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Face => await QueryFaceIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Page => await QueryPageIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Album => await QueryAlbumIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Blog => await QueryBlogIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Reel => await QueryReelIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.Story => await QueryStoryIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.FaceChatRoom => await QueryChatRoomIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.VideoLounge => await QueryLoungeIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.FaceProfile => await QueryFaceProfileIdsAsync(ids, cancellationToken),
                SearchDocumentTypes.WallTicket => await QueryWallTicketIdsAsync(ids, cancellationToken),
                _ => [],
            };
        }

        return result;
    }

    private static List<int> ParseIntIds(IReadOnlyList<string> ids) =>
        ids.Select(id => int.TryParse(id, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();

    private static async Task<HashSet<string>> ToIdSetAsync(
        IQueryable<int> query,
        CancellationToken cancellationToken)
    {
        var found = await query.ToListAsync(cancellationToken);
        return found.Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HashSet<string>> QueryFaceIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(_db.Faces.AsNoTracking().Where(f => parsed.Contains(f.Id)).Select(f => f.Id), ct);
    }

    private async Task<HashSet<string>> QueryPageIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(_db.Pages.AsNoTracking().Where(p => parsed.Contains(p.Id)).Select(p => p.Id), ct);
    }

    private async Task<HashSet<string>> QueryAlbumIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.Albums.AsNoTracking().Where(a => a.RemovedAtUtc == null && parsed.Contains(a.Id)).Select(a => a.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryBlogIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.Blogs.AsNoTracking().Where(b => b.RemovedAtUtc == null && parsed.Contains(b.Id)).Select(b => b.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryReelIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.Reels.AsNoTracking().Where(r => r.RemovedAtUtc == null && parsed.Contains(r.Id)).Select(r => r.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryStoryIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(_db.Stories.AsNoTracking().Where(s => parsed.Contains(s.Id)).Select(s => s.Id), ct);
    }

    private async Task<HashSet<string>> QueryChatRoomIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.FaceChatRooms.AsNoTracking().Where(r => parsed.Contains(r.Id)).Select(r => r.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryLoungeIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.FaceVideoLounges.AsNoTracking().Where(l => parsed.Contains(l.Id)).Select(l => l.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryFaceProfileIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.UserFaceProfiles.AsNoTracking().Where(p => p.IsActive && parsed.Contains(p.Id)).Select(p => p.Id),
                ct);
    }

    private async Task<HashSet<string>> QueryWallTicketIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var parsed = ParseIntIds(ids);
        return parsed.Count == 0
            ? []
            : await ToIdSetAsync(
                _db.FaceWallTickets.AsNoTracking().Where(t => parsed.Contains(t.Id)).Select(t => t.Id),
                ct);
    }

    private async Task<HashSet<string>> FilterUserIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.LockoutEnd })
            .ToListAsync(cancellationToken);

        return users
            .Where(u => SearchIndexVisibility.IsUserIndexable(
                new ApplicationUser { Id = u.Id, Email = u.Email },
                u.LockoutEnd))
            .Select(u => u.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, string>> LoadFaceProfileUserIdsAsync(
        IReadOnlyList<AutocompleteHit> hits,
        CancellationToken cancellationToken)
    {
        var profileIds = ParseIntIds(
            hits.Where(h => h.DocumentType == SearchDocumentTypes.FaceProfile).Select(h => h.EntityId).ToList());

        if (profileIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var rows = await _db.UserFaceProfiles.AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .Join(
                _db.UserProfiles.AsNoTracking(),
                p => p.UserProfileId,
                up => up.Id,
                (p, up) => new { ProfileId = p.Id, up.UserId })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ProfileId.ToString(), r => r.UserId, StringComparer.Ordinal);
    }
}
