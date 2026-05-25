using BeDemo.Api.Data;
using BeDemo.Api.Models;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services.Search;

/// <summary>Builds worker <see cref="SearchDocument"/> projections from PostgreSQL entities.</summary>
public sealed class SearchDocumentBuilder
{
    private readonly ApplicationDbContext _db;

    public SearchDocumentBuilder(ApplicationDbContext db) => _db = db;

    public static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    public SearchDocument? FromUser(ApplicationUser user, DateTimeOffset? lockoutEnd)
    {
        if (!SearchIndexVisibility.IsUserIndexable(user, lockoutEnd))
            return null;

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.User,
            EntityId = user.Id,
            Title = user.Email ?? user.UserName ?? user.Id,
            Subtitle = string.IsNullOrWhiteSpace(name) ? string.Empty : name,
            SearchText = $"{user.Email} {name} {user.UserName}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(user.CreatedAt),
        };
    }

    public SearchDocument FromFace(Face face) =>
        new()
        {
            DocumentType = SearchDocumentTypes.Face,
            EntityId = face.Id.ToString(),
            Title = face.Title,
            Subtitle = face.Index,
            SearchText = $"{face.Title} {face.Index} {face.Description}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(face.UpdatedAt ?? face.CreatedAt),
        };

    public SearchDocument FromPage(Page page) =>
        new()
        {
            DocumentType = SearchDocumentTypes.Page,
            EntityId = page.Id.ToString(),
            FaceId = page.FaceId.ToString(),
            Title = page.Name,
            Subtitle = page.Path,
            SearchText = $"{page.Name} {page.Path} {page.Description}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(page.UpdatedAt ?? page.CreatedAt),
        };

    public SearchDocument? FromAlbum(Album album, int? primaryFaceId)
    {
        if (!SearchIndexVisibility.IsAlbumIndexable(album))
            return null;

        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.Album,
            EntityId = album.Id.ToString(),
            FaceId = primaryFaceId?.ToString() ?? string.Empty,
            Title = album.Title,
            Subtitle = album.Description ?? string.Empty,
            SearchText = $"{album.Title} {album.Description}".Trim(),
            ApprovalStatus = album.ApprovalStatus.ToString(),
            UpdatedAtUnixMs = ToUnixMs(album.UpdatedAt ?? album.CreatedAt),
        };
    }

    public SearchDocument? FromBlog(Blog blog)
    {
        if (!SearchIndexVisibility.IsBlogIndexable(blog))
            return null;

        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.Blog,
            EntityId = blog.Id.ToString(),
            FaceId = blog.FaceId.ToString(),
            Title = blog.Title,
            Subtitle = blog.Content.Length > 120 ? blog.Content[..120] : blog.Content,
            SearchText = $"{blog.Title} {blog.Content}".Trim(),
            ApprovalStatus = blog.ApprovalStatus.ToString(),
            UpdatedAtUnixMs = ToUnixMs(blog.UpdatedAt ?? blog.CreatedAt),
        };
    }

    public SearchDocument? FromReel(Reel reel, int? primaryFaceId)
    {
        if (!SearchIndexVisibility.IsReelIndexable(reel))
            return null;

        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.Reel,
            EntityId = reel.Id.ToString(),
            FaceId = primaryFaceId?.ToString() ?? string.Empty,
            Title = reel.Title,
            Subtitle = reel.Description ?? string.Empty,
            SearchText = $"{reel.Title} {reel.Description}".Trim(),
            ApprovalStatus = reel.ApprovalStatus.ToString(),
            UpdatedAtUnixMs = ToUnixMs(reel.UpdatedAt ?? reel.CreatedAt),
        };
    }

    public SearchDocument? FromStory(Story story, int? primaryFaceId)
    {
        if (!SearchIndexVisibility.IsStoryIndexable(story))
            return null;

        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.Story,
            EntityId = story.Id.ToString(),
            FaceId = primaryFaceId?.ToString() ?? string.Empty,
            Title = story.Title,
            Subtitle = story.State.ToString(),
            SearchText = story.Title,
            UpdatedAtUnixMs = ToUnixMs(story.UpdatedAt ?? story.CreatedAt),
        };
    }

    public SearchDocument FromFaceChatRoom(FaceChatRoom room) =>
        new()
        {
            DocumentType = SearchDocumentTypes.FaceChatRoom,
            EntityId = room.Id.ToString(),
            FaceId = room.FaceId.ToString(),
            Title = room.Title,
            Subtitle = room.Description ?? string.Empty,
            SearchText = $"{room.Title} {room.Description}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(room.UpdatedAt ?? room.CreatedAt),
        };

    public SearchDocument FromVideoLounge(FaceVideoLounge lounge) =>
        new()
        {
            DocumentType = SearchDocumentTypes.VideoLounge,
            EntityId = lounge.Id.ToString(),
            FaceId = lounge.FaceId.ToString(),
            Title = lounge.Title,
            Subtitle = lounge.Description ?? string.Empty,
            SearchText = $"{lounge.Title} {lounge.Description}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(lounge.UpdatedAt ?? lounge.CreatedAt),
        };

    public SearchDocument? FromFaceProfile(UserFaceProfile profile, ApplicationUser user, UserProfile? globalProfile)
    {
        if (!SearchIndexVisibility.IsFaceProfileIndexable(profile))
            return null;

        var display = profile.DisplayName
            ?? globalProfile?.Nickname
            ?? user.Email
            ?? user.Id;

        return new SearchDocument
        {
            DocumentType = SearchDocumentTypes.FaceProfile,
            EntityId = profile.Id.ToString(),
            FaceId = profile.FaceId.ToString(),
            RoutingUserId = user.Id,
            Title = display,
            Subtitle = user.Email ?? string.Empty,
            SearchText = $"{display} {user.Email} {user.UserName}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(profile.UpdatedAt ?? profile.CreatedAt),
        };
    }

    public SearchDocument FromWallTicket(FaceWallTicket ticket) =>
        new()
        {
            DocumentType = SearchDocumentTypes.WallTicket,
            EntityId = ticket.Id.ToString(),
            FaceId = ticket.FaceId.ToString(),
            Title = ticket.Title,
            Subtitle = ticket.Status.ToString(),
            SearchText = $"{ticket.Title} {ticket.Description}".Trim(),
            UpdatedAtUnixMs = ToUnixMs(ticket.UpdatedAt ?? ticket.CreatedAt),
        };

    /// <summary>Reads indexable entity ids for orphan cleanup diff per document type.</summary>
    public async Task<HashSet<string>> GetIndexableEntityIdsAsync(string documentType, CancellationToken cancellationToken)
    {
        return documentType switch
        {
            SearchDocumentTypes.User => (await _db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.Email, u.LockoutEnd })
                .ToListAsync(cancellationToken))
                .Where(u => SearchIndexVisibility.IsUserIndexable(
                    new ApplicationUser { Id = u.Id, Email = u.Email },
                    u.LockoutEnd))
                .Select(u => u.Id)
                .ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Face => (await _db.Faces.AsNoTracking().Select(f => f.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Page => (await _db.Pages.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Album => (await _db.Albums.AsNoTracking()
                .Where(a => a.RemovedAtUtc == null).Select(a => a.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Blog => (await _db.Blogs.AsNoTracking()
                .Where(b => b.RemovedAtUtc == null).Select(b => b.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Reel => (await _db.Reels.AsNoTracking()
                .Where(r => r.RemovedAtUtc == null).Select(r => r.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.Story => (await _db.Stories.AsNoTracking().Select(s => s.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.FaceChatRoom => (await _db.FaceChatRooms.AsNoTracking().Select(r => r.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.VideoLounge => (await _db.FaceVideoLounges.AsNoTracking().Select(l => l.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.FaceProfile => (await _db.UserFaceProfiles.AsNoTracking()
                .Where(p => p.IsActive).Select(p => p.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            SearchDocumentTypes.WallTicket => (await _db.FaceWallTickets.AsNoTracking().Select(t => t.Id).ToListAsync(cancellationToken))
                .Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal),
            _ => [],
        };
    }

    /// <summary>Paginated indexable documents for reconciliation batch upsert.</summary>
    public async Task<IReadOnlyList<SearchDocument>> ReadIndexableBatchAsync(
        string documentType,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return documentType switch
        {
            SearchDocumentTypes.User => await ReadUsersBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Face => await ReadFacesBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Page => await ReadPagesBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Album => await ReadAlbumsBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Blog => await ReadBlogsBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Reel => await ReadReelsBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.Story => await ReadStoriesBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.FaceChatRoom => await ReadChatRoomsBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.VideoLounge => await ReadLoungesBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.FaceProfile => await ReadFaceProfilesBatchAsync(skip, take, cancellationToken),
            SearchDocumentTypes.WallTicket => await ReadWallTicketsBatchAsync(skip, take, cancellationToken),
            _ => [],
        };
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadUsersBatchAsync(int skip, int take, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.Id)
            .Skip(skip).Take(take)
            .Select(u => new { User = u, u.LockoutEnd })
            .ToListAsync(ct);
        return users
            .Select(u => FromUser(u.User, u.LockoutEnd))
            .Where(d => d is not null)
            .Cast<SearchDocument>()
            .ToList();
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadFacesBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.Faces.AsNoTracking().OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromFace).ToList();

    private async Task<IReadOnlyList<SearchDocument>> ReadPagesBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.Pages.AsNoTracking().OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromPage).ToList();

    private async Task<IReadOnlyList<SearchDocument>> ReadAlbumsBatchAsync(int skip, int take, CancellationToken ct)
    {
        var albums = await _db.Albums.AsNoTracking()
            .Where(a => a.RemovedAtUtc == null)
            .OrderBy(a => a.Id).Skip(skip).Take(take)
            .ToListAsync(ct);
        var faceMap = await PrimaryFaceIdsForAlbumsAsync(albums.Select(a => a.Id), ct);
        return albums.Select(a => FromAlbum(a, faceMap.GetValueOrDefault(a.Id))).Where(d => d is not null).Cast<SearchDocument>().ToList();
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadBlogsBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.Blogs.AsNoTracking().Where(b => b.RemovedAtUtc == null).OrderBy(b => b.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromBlog).Where(d => d is not null).Cast<SearchDocument>().ToList();

    private async Task<IReadOnlyList<SearchDocument>> ReadReelsBatchAsync(int skip, int take, CancellationToken ct)
    {
        var reels = await _db.Reels.AsNoTracking()
            .Where(r => r.RemovedAtUtc == null)
            .OrderBy(r => r.Id).Skip(skip).Take(take)
            .ToListAsync(ct);
        var faceMap = await PrimaryFaceIdsForReelsAsync(reels.Select(r => r.Id), ct);
        return reels.Select(r => FromReel(r, faceMap.GetValueOrDefault(r.Id))).Where(d => d is not null).Cast<SearchDocument>().ToList();
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadStoriesBatchAsync(int skip, int take, CancellationToken ct)
    {
        var stories = await _db.Stories.AsNoTracking()
            .OrderBy(s => s.Id).Skip(skip).Take(take)
            .ToListAsync(ct);
        var faceMap = await PrimaryFaceIdsForStoriesAsync(stories.Select(s => s.Id), ct);
        return stories.Select(s => FromStory(s, faceMap.GetValueOrDefault(s.Id))).Where(d => d is not null).Cast<SearchDocument>().ToList();
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadChatRoomsBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.FaceChatRooms.AsNoTracking().OrderBy(r => r.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromFaceChatRoom).ToList();

    private async Task<IReadOnlyList<SearchDocument>> ReadLoungesBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.FaceVideoLounges.AsNoTracking().OrderBy(l => l.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromVideoLounge).ToList();

    private async Task<IReadOnlyList<SearchDocument>> ReadFaceProfilesBatchAsync(int skip, int take, CancellationToken ct)
    {
        var profiles = await _db.UserFaceProfiles.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id).Skip(skip).Take(take)
            .Include(p => p.UserProfile).ThenInclude(up => up.User)
            .ToListAsync(ct);
        return profiles
            .Select(p => FromFaceProfile(p, p.UserProfile.User, p.UserProfile))
            .Where(d => d is not null)
            .Cast<SearchDocument>()
            .ToList();
    }

    private async Task<IReadOnlyList<SearchDocument>> ReadWallTicketsBatchAsync(int skip, int take, CancellationToken ct) =>
        (await _db.FaceWallTickets.AsNoTracking().OrderBy(t => t.Id).Skip(skip).Take(take).ToListAsync(ct))
        .Select(FromWallTicket).ToList();

    private async Task<Dictionary<int, int>> PrimaryFaceIdsForAlbumsAsync(IEnumerable<int> albumIds, CancellationToken ct)
    {
        var ids = albumIds.ToList();
        if (ids.Count == 0) return [];
        return await _db.AlbumFaces.AsNoTracking()
            .Where(af => ids.Contains(af.AlbumId))
            .GroupBy(af => af.AlbumId)
            .Select(g => new { AlbumId = g.Key, FaceId = g.Min(af => af.FaceId) })
            .ToDictionaryAsync(x => x.AlbumId, x => x.FaceId, ct);
    }

    private async Task<Dictionary<int, int>> PrimaryFaceIdsForReelsAsync(IEnumerable<int> reelIds, CancellationToken ct)
    {
        var ids = reelIds.ToList();
        if (ids.Count == 0) return [];
        return await _db.ReelFaces.AsNoTracking()
            .Where(rf => ids.Contains(rf.ReelId))
            .GroupBy(rf => rf.ReelId)
            .Select(g => new { ReelId = g.Key, FaceId = g.Min(rf => rf.FaceId) })
            .ToDictionaryAsync(x => x.ReelId, x => x.FaceId, ct);
    }

    private async Task<Dictionary<int, int>> PrimaryFaceIdsForStoriesAsync(IEnumerable<int> storyIds, CancellationToken ct)
    {
        var ids = storyIds.ToList();
        if (ids.Count == 0) return [];
        return await _db.StoryFaces.AsNoTracking()
            .Where(sf => ids.Contains(sf.StoryId))
            .GroupBy(sf => sf.StoryId)
            .Select(g => new { StoryId = g.Key, FaceId = g.Min(sf => sf.FaceId) })
            .ToDictionaryAsync(x => x.StoryId, x => x.FaceId, ct);
    }
}
