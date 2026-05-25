using BeDemo.Api.Data;
using BeDemo.Api.Models;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>Drains <see cref="SearchOutboxEntry"/> rows to the search worker (§6.1).</summary>
public sealed class SearchOutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchOutboxProcessorHostedService> _logger;

    public SearchOutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SearchOptions> options,
        ILogger<SearchOutboxProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.IsEnabled)
            return;

        var pollSeconds = Math.Max(1, _options.Value.OutboxPollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search outbox processor tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ISearchQueryGateway>();
        var builder = scope.ServiceProvider.GetRequiredService<SearchDocumentBuilder>();
        var opts = _options.Value;

        if (!opts.IsEnabled || !gateway.IsAvailable)
            return;

        var pendingCount = await db.SearchOutboxEntries.CountAsync(e => e.ProcessedAtUtc == null, cancellationToken);
        SearchObservability.LogOutboxPendingCount(_logger, pendingCount, opts.OutboxWarningPendingCount);

        var batch = await db.SearchOutboxEntries
            .Where(e => e.ProcessedAtUtc == null)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var entry in batch)
        {
            try
            {
                if (entry.Operation == SearchOutboxOperation.Delete)
                {
                    var response = await gateway.DeleteDocumentAsync(new DeleteDocumentRequest
                    {
                        DocumentType = entry.DocumentType,
                        EntityId = entry.EntityId,
                        CorrelationId = SearchWorkerGrpcGateway.NewCorrelationId(),
                    }, cancellationToken);

                    if (response is null || !response.Success)
                        throw new InvalidOperationException(response?.ErrorMessage ?? "DeleteDocument returned null");
                }
                else
                {
                    var document = await BuildDocumentForEntryAsync(db, builder, entry, cancellationToken);
                    if (document is null)
                    {
                        // Row no longer indexable — treat as delete.
                        var del = await gateway.DeleteDocumentAsync(new DeleteDocumentRequest
                        {
                            DocumentType = entry.DocumentType,
                            EntityId = entry.EntityId,
                            CorrelationId = SearchWorkerGrpcGateway.NewCorrelationId(),
                        }, cancellationToken);
                        if (del is null || !del.Success)
                            throw new InvalidOperationException(del?.ErrorMessage ?? "DeleteDocument returned null");
                    }
                    else
                    {
                        var response = await gateway.IndexDocumentAsync(new IndexDocumentRequest
                        {
                            Document = document,
                            CorrelationId = SearchWorkerGrpcGateway.NewCorrelationId(),
                        }, cancellationToken);
                        if (response is null || !response.Success)
                            throw new InvalidOperationException(response?.ErrorMessage ?? "IndexDocument returned null");
                    }
                }

                entry.ProcessedAtUtc = DateTime.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex)
            {
                entry.AttemptCount++;
                entry.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                _logger.LogWarning(
                    ex,
                    "Search outbox entry failed documentType={DocumentType} entityId={EntityId} attempt={Attempt}",
                    entry.DocumentType,
                    entry.EntityId,
                    entry.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<SearchDocument?> BuildDocumentForEntryAsync(
        ApplicationDbContext db,
        SearchDocumentBuilder builder,
        SearchOutboxEntry entry,
        CancellationToken cancellationToken)
    {
        return entry.DocumentType switch
        {
            SearchDocumentTypes.User => await BuildUserDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Face => await BuildFaceDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Page => await BuildPageDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Album => await BuildAlbumDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Blog => await BuildBlogDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Reel => await BuildReelDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.Story => await BuildStoryDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.FaceChatRoom => await BuildChatRoomDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.VideoLounge => await BuildLoungeDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.FaceProfile => await BuildFaceProfileDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            SearchDocumentTypes.WallTicket => await BuildWallTicketDocumentAsync(db, builder, entry.EntityId, cancellationToken),
            _ => null,
        };
    }

    private static async Task<SearchDocument?> BuildUserDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : builder.FromUser(user, user.LockoutEnd);
    }

    private static async Task<SearchDocument?> BuildFaceDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var faceId)) return null;
        var face = await db.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId, ct);
        return face is null ? null : builder.FromFace(face);
    }

    private static async Task<SearchDocument?> BuildPageDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var pageId)) return null;
        var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pageId, ct);
        return page is null ? null : builder.FromPage(page);
    }

    private static async Task<SearchDocument?> BuildAlbumDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var albumId)) return null;
        var album = await db.Albums.AsNoTracking().FirstOrDefaultAsync(a => a.Id == albumId, ct);
        if (album is null) return null;
        var faceId = await db.AlbumFaces.AsNoTracking()
            .Where(af => af.AlbumId == albumId).Select(af => (int?)af.FaceId).FirstOrDefaultAsync(ct);
        return builder.FromAlbum(album, faceId);
    }

    private static async Task<SearchDocument?> BuildBlogDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var blogId)) return null;
        var blog = await db.Blogs.AsNoTracking().FirstOrDefaultAsync(b => b.Id == blogId, ct);
        return blog is null ? null : builder.FromBlog(blog);
    }

    private static async Task<SearchDocument?> BuildReelDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var reelId)) return null;
        var reel = await db.Reels.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reelId, ct);
        if (reel is null) return null;
        var faceId = await db.ReelFaces.AsNoTracking()
            .Where(rf => rf.ReelId == reelId).Select(rf => (int?)rf.FaceId).FirstOrDefaultAsync(ct);
        return builder.FromReel(reel, faceId);
    }

    private static async Task<SearchDocument?> BuildStoryDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var storyId)) return null;
        var story = await db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storyId, ct);
        if (story is null) return null;
        var faceId = await db.StoryFaces.AsNoTracking()
            .Where(sf => sf.StoryId == storyId).Select(sf => (int?)sf.FaceId).FirstOrDefaultAsync(ct);
        return builder.FromStory(story, faceId);
    }

    private static async Task<SearchDocument?> BuildChatRoomDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var roomId)) return null;
        var room = await db.FaceChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        return room is null ? null : builder.FromFaceChatRoom(room);
    }

    private static async Task<SearchDocument?> BuildLoungeDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var loungeId)) return null;
        var lounge = await db.FaceVideoLounges.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loungeId, ct);
        return lounge is null ? null : builder.FromVideoLounge(lounge);
    }

    private static async Task<SearchDocument?> BuildFaceProfileDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var profileId)) return null;
        var profile = await db.UserFaceProfiles.AsNoTracking()
            .Include(p => p.UserProfile).ThenInclude(up => up.User)
            .FirstOrDefaultAsync(p => p.Id == profileId, ct);
        return profile is null ? null : builder.FromFaceProfile(profile, profile.UserProfile.User, profile.UserProfile);
    }

    private static async Task<SearchDocument?> BuildWallTicketDocumentAsync(
        ApplicationDbContext db, SearchDocumentBuilder builder, string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var ticketId)) return null;
        var ticket = await db.FaceWallTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        return ticket is null ? null : builder.FromWallTicket(ticket);
    }
}
