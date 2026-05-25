using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BeDemo.Api.Services.Search;

/// <summary>Shared outbox upsert logic for <see cref="SearchOutboxService"/> and the save interceptor.</summary>
internal static class SearchOutboxStaging
{
    internal static void StageIndex(ApplicationDbContext db, string documentType, string entityId) =>
        Upsert(db, documentType, entityId, SearchOutboxOperation.Index);

    internal static void StageDelete(ApplicationDbContext db, string documentType, string entityId) =>
        Upsert(db, documentType, entityId, SearchOutboxOperation.Delete);

    private static void Upsert(ApplicationDbContext db, string documentType, string entityId, SearchOutboxOperation operation)
    {
        if (string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(entityId))
            return;

        var existing = db.SearchOutboxEntries
            .FirstOrDefault(e => e.DocumentType == documentType && e.EntityId == entityId && e.ProcessedAtUtc == null);

        if (existing is not null)
        {
            existing.Operation = operation;
            existing.CreatedAtUtc = DateTime.UtcNow;
            existing.AttemptCount = 0;
            existing.LastError = null;
            return;
        }

        db.SearchOutboxEntries.Add(new SearchOutboxEntry
        {
            DocumentType = documentType,
            EntityId = entityId,
            Operation = operation,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}

/// <summary>Maps EF entities to search document types for outbox staging.</summary>
internal static class SearchOutboxEntityMapper
{
    internal static bool TryMap(EntityEntry entry, out string documentType, out string entityId)
    {
        documentType = string.Empty;
        entityId = string.Empty;

        switch (entry.Entity)
        {
            case ApplicationUser user:
                documentType = SearchDocumentTypes.User;
                entityId = user.Id;
                return true;
            case Face face:
                documentType = SearchDocumentTypes.Face;
                entityId = face.Id.ToString();
                return true;
            case Page page:
                documentType = SearchDocumentTypes.Page;
                entityId = page.Id.ToString();
                return true;
            case Album album:
                documentType = SearchDocumentTypes.Album;
                entityId = album.Id.ToString();
                return true;
            case Blog blog:
                documentType = SearchDocumentTypes.Blog;
                entityId = blog.Id.ToString();
                return true;
            case Reel reel:
                documentType = SearchDocumentTypes.Reel;
                entityId = reel.Id.ToString();
                return true;
            case Story story:
                documentType = SearchDocumentTypes.Story;
                entityId = story.Id.ToString();
                return true;
            case FaceChatRoom room:
                documentType = SearchDocumentTypes.FaceChatRoom;
                entityId = room.Id.ToString();
                return true;
            case FaceVideoLounge lounge:
                documentType = SearchDocumentTypes.VideoLounge;
                entityId = lounge.Id.ToString();
                return true;
            case UserFaceProfile profile:
                documentType = SearchDocumentTypes.FaceProfile;
                entityId = profile.Id.ToString();
                return true;
            case FaceWallTicket ticket:
                documentType = SearchDocumentTypes.WallTicket;
                entityId = ticket.Id.ToString();
                return true;
            default:
                return false;
        }
    }

    internal static bool ShouldIndex(EntityEntry entry)
    {
        return entry.Entity switch
        {
            ApplicationUser user => SearchIndexVisibility.IsUserIndexable(user, user.LockoutEnd),
            Face face => SearchIndexVisibility.IsFaceIndexable(face),
            Page page => SearchIndexVisibility.IsPageIndexable(page),
            Album album => SearchIndexVisibility.IsAlbumIndexable(album),
            Blog blog => SearchIndexVisibility.IsBlogIndexable(blog),
            Reel reel => SearchIndexVisibility.IsReelIndexable(reel),
            Story story => SearchIndexVisibility.IsStoryIndexable(story),
            FaceChatRoom room => SearchIndexVisibility.IsFaceChatRoomIndexable(room),
            FaceVideoLounge lounge => SearchIndexVisibility.IsVideoLoungeIndexable(lounge),
            UserFaceProfile profile => SearchIndexVisibility.IsFaceProfileIndexable(profile),
            FaceWallTicket ticket => SearchIndexVisibility.IsWallTicketIndexable(ticket),
            _ => false,
        };
    }
}
