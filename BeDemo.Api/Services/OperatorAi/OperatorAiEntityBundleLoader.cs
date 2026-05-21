using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Loads one entity bundle by stable catalog index (stage 1 prefetch — DB aggregates only, no PII).
/// </summary>
public interface IOperatorAiEntityBundleLoader
{
    Task<OperatorAiEntityBundleDto> LoadAsync(int index, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiEntityBundleLoader : IOperatorAiEntityBundleLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly OperatorAiOptions _options;

    public OperatorAiEntityBundleLoader(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<OperatorAiOptions> options)
    {
        _contextFactory = contextFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<OperatorAiEntityBundleDto> LoadAsync(int index, CancellationToken cancellationToken = default)
    {
        var meta = OperatorAiEntityBundleCatalog.GetByIndex(index);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = DateTime.UtcNow;
        var dto = index switch
        {
            0 => await LoadUsersAsync(context, meta, snapshot, cancellationToken),
            1 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserProfiles, cancellationToken),
            2 => await LoadUserRolesAsync(context, meta, snapshot, cancellationToken),
            3 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserFaceRoles, cancellationToken),
            4 => await LoadRegistrationInvitesAsync(context, meta, snapshot, cancellationToken),
            5 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserPushDevices, cancellationToken),
            6 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.Friendships, f => f.CreatedAt, cancellationToken),
            7 => await LoadFriendRequestsAsync(context, meta, snapshot, cancellationToken),
            8 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserFollows, cancellationToken),
            9 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserBlocks, cancellationToken),
            10 => await LoadMessagesAsync(context, meta, snapshot, cancellationToken),
            11 => await LoadNotificationsAsync(context, meta, snapshot, cancellationToken),
            12 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.Faces, cancellationToken),
            13 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.Pages, cancellationToken),
            14 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.PageTypes, cancellationToken),
            15 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.PageComponents, cancellationToken),
            16 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.PageRouteTranslations, cancellationToken),
            17 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.ComponentTypes, cancellationToken),
            18 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.DisplayModes, cancellationToken),
            19 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserFaceProfiles, cancellationToken),
            20 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.UserFaceProfileComments, c => c.CreatedAt, cancellationToken),
            21 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserFaceProfileLikes, cancellationToken),
            22 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.UserFaceProfileReviews, cancellationToken),
            23 => await LoadUgcAsync(context, meta, snapshot, ctx => ctx.Albums, a => a.ApprovalStatus, a => a.AiReviewStatus, a => a.CreatedAt, cancellationToken),
            24 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.AlbumFaces, cancellationToken),
            25 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.AlbumMedia, cancellationToken),
            26 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.AlbumComments, c => c.CreatedAt, cancellationToken),
            27 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.AlbumLikes, cancellationToken),
            28 => await LoadUgcAsync(context, meta, snapshot, ctx => ctx.Blogs, b => b.ApprovalStatus, b => b.AiReviewStatus, b => b.CreatedAt, cancellationToken),
            29 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.BlogImages, cancellationToken),
            30 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.BlogComments, c => c.CreatedAt, cancellationToken),
            31 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.BlogLikes, cancellationToken),
            32 => await LoadUgcAsync(context, meta, snapshot, ctx => ctx.Reels, r => r.ApprovalStatus, r => r.AiReviewStatus, r => r.CreatedAt, cancellationToken),
            33 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.ReelFaces, cancellationToken),
            34 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.ReelComments, c => c.CreatedAt, cancellationToken),
            35 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.ReelLikes, cancellationToken),
            36 => await LoadStoriesAsync(context, meta, snapshot, cancellationToken),
            37 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.StoryFaces, cancellationToken),
            38 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.StoryImages, cancellationToken),
            39 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.StoryComments, c => c.CreatedAt, cancellationToken),
            40 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.StoryLikes, cancellationToken),
            41 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.StoryViews, v => v.ViewedAt, cancellationToken),
            42 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceChatRooms, cancellationToken),
            43 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceChatRoomMembers, cancellationToken),
            44 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.FaceChatRoomMessages, m => m.SentAt, cancellationToken),
            45 => await LoadChatRoomJoinRequestsAsync(context, meta, snapshot, cancellationToken),
            46 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceVideoLounges, cancellationToken),
            47 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceVideoLoungeMembers, cancellationToken),
            48 => await LoadVideoLoungeJoinRequestsAsync(context, meta, snapshot, cancellationToken),
            49 => await LoadVideoLoungeSessionsAsync(context, meta, snapshot, cancellationToken),
            50 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceVideoLoungeSessionParticipants, cancellationToken),
            51 => await LoadWallTicketsAsync(context, meta, snapshot, cancellationToken),
            52 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.FaceWallTicketComments, c => c.CreatedAt, cancellationToken),
            53 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.FaceWallTicketLikes, cancellationToken),
            54 => await LoadAiReviewJobsAsync(context, meta, snapshot, cancellationToken),
            55 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.ContentModerationEvents, e => e.CreatedAtUtc, cancellationToken),
            56 => await LoadUserFaceModerationsAsync(context, meta, snapshot, cancellationToken),
            57 => await CountOnlyAsync(context, meta, snapshot, ctx => ctx.OAuthClients, cancellationToken),
            58 => await LoadOAuthRefreshTokensAsync(context, meta, snapshot, cancellationToken),
            59 => await WithTimeseriesAsync(context, meta, snapshot, ctx => ctx.OperatorAiConversations, c => c.CreatedAt, cancellationToken),
            60 => await LoadOperatorAiMessagesAsync(context, meta, snapshot, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return dto;
    }

    public static string Serialize(OperatorAiEntityBundleDto dto) =>
        JsonSerializer.Serialize(dto, JsonOptions);

    private async Task<OperatorAiEntityBundleDto> CountOnlyAsync<TEntity>(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSet,
        CancellationToken ct) where TEntity : class
    {
        var total = await dbSet(context).AsNoTracking().CountAsync(ct);
        return Merge(meta, snapshot, total);
    }

    private async Task<OperatorAiEntityBundleDto> WithTimeseriesAsync<TEntity>(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSet,
        Expression<Func<TEntity, DateTime>> timestamp,
        CancellationToken ct) where TEntity : class
    {
        var total = await dbSet(context).AsNoTracking().CountAsync(ct);
        var series = await BuildTimeseriesAsync(dbSet(context).AsNoTracking(), timestamp, ct);
        return Merge(meta, snapshot, total, timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadUsersAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var users = context.Users.AsNoTracking();
        var total = await users.CountAsync(ct);
        var confirmed = await users.CountAsync(u => u.EmailConfirmed, ct);
        var series = await BuildTimeseriesAsync(users, u => u.CreatedAt, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["confirmed"] = confirmed,
                ["unconfirmed"] = total - confirmed,
            },
            timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadUserRolesAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.UserRoles.AsNoTracking().CountAsync(ct);
        var groups = await context.UserRoles.AsNoTracking()
            .GroupBy(r => r.Name)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byName = groups.ToDictionary(g => g.Key ?? "unknown", g => g.Count, StringComparer.Ordinal);
        return Merge(meta, snapshot, total, byType: byName);
    }

    private async Task<OperatorAiEntityBundleDto> LoadRegistrationInvitesAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = context.RegistrationInvites.AsNoTracking();
        var total = await q.CountAsync(ct);
        var consumed = await q.CountAsync(i => i.ConsumedAtUtc != null, ct);
        var revoked = await q.CountAsync(i => i.RevokedAtUtc != null, ct);
        var expired = await q.CountAsync(i => i.ConsumedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc <= now, ct);
        var pending = total - consumed - revoked - expired;
        var series = await BuildTimeseriesAsync(q, i => i.CreatedAtUtc, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["pending"] = pending,
                ["consumed"] = consumed,
                ["expired"] = expired,
                ["revoked"] = revoked,
            },
            timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadFriendRequestsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.FriendRequests.AsNoTracking().CountAsync(ct);
        var byStatus = await EnumCountsAsync(
            context.FriendRequests.AsNoTracking(),
            r => r.Status,
            ct);
        return Merge(meta, snapshot, total, byStatus: byStatus);
    }

    private async Task<OperatorAiEntityBundleDto> LoadMessagesAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.Messages.AsNoTracking().CountAsync(ct);
        var pending = await context.Messages.AsNoTracking()
            .CountAsync(m => m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int> { ["pendingMessageRequests"] = pending });
    }

    private async Task<OperatorAiEntityBundleDto> LoadNotificationsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.Notifications.AsNoTracking().CountAsync(ct);
        var byType = await context.Notifications.AsNoTracking()
            .GroupBy(n => n.Type)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count, ct);
        return Merge(meta, snapshot, total, byType: byType);
    }

    private async Task<OperatorAiEntityBundleDto> LoadUgcAsync<T>(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        Func<ApplicationDbContext, DbSet<T>> dbSet,
        Expression<Func<T, ContentApprovalStatus>> approval,
        Expression<Func<T, AiReviewStatus>> aiReview,
        Expression<Func<T, DateTime>> createdAt,
        CancellationToken ct) where T : class
    {
        var set = dbSet(context).AsNoTracking();
        var total = await set.CountAsync(ct);
        var byApproval = await EnumCountsAsync(set, approval, ct);
        var byAi = await EnumCountsAsync(set, aiReview, ct);
        var series = await BuildTimeseriesAsync(set, createdAt, ct);
        return Merge(meta, snapshot, total, byStatus: byApproval, byAiReviewStatus: byAi, timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadStoriesAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = context.Stories.AsNoTracking();
        var total = await q.CountAsync(ct);
        var byState = await EnumCountsAsync(q, s => s.State, ct);
        var liveNow = await q.CountAsync(
            s => s.PublishedAt != null && s.PublishedAt <= now && s.ExpiresAt != null && s.ExpiresAt > now,
            ct);
        var expired = await q.CountAsync(s => s.ExpiresAt != null && s.ExpiresAt <= now, ct);
        var scheduled = await q.CountAsync(s => s.ScheduledPublishAt != null && s.ScheduledPublishAt > now, ct);
        byState["liveNow"] = liveNow;
        byState["expired"] = expired;
        byState["scheduled"] = scheduled;
        var series = await BuildTimeseriesAsync(q, s => s.CreatedAt, ct);
        return Merge(meta, snapshot, total, byStatus: byState, timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadChatRoomJoinRequestsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.FaceChatRoomJoinRequests.AsNoTracking().CountAsync(ct);
        var byStatus = await EnumCountsAsync(
            context.FaceChatRoomJoinRequests.AsNoTracking(),
            j => j.Status,
            ct);
        return Merge(meta, snapshot, total, byStatus: byStatus);
    }

    private async Task<OperatorAiEntityBundleDto> LoadVideoLoungeJoinRequestsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.FaceVideoLoungeJoinRequests.AsNoTracking().CountAsync(ct);
        var byStatus = await EnumCountsAsync(
            context.FaceVideoLoungeJoinRequests.AsNoTracking(),
            j => j.Status,
            ct);
        return Merge(meta, snapshot, total, byStatus: byStatus);
    }

    private async Task<OperatorAiEntityBundleDto> LoadVideoLoungeSessionsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var q = context.FaceVideoLoungeSessions.AsNoTracking();
        var total = await q.CountAsync(ct);
        var liveNow = await q.CountAsync(s => s.EndedAt == null, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["liveNow"] = liveNow,
                ["ended"] = total - liveNow,
            });
    }

    private async Task<OperatorAiEntityBundleDto> LoadWallTicketsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.FaceWallTickets.AsNoTracking().CountAsync(ct);
        var byStatus = await EnumCountsAsync(
            context.FaceWallTickets.AsNoTracking(),
            t => t.Status,
            ct);
        return Merge(meta, snapshot, total, byStatus: byStatus);
    }

    private async Task<OperatorAiEntityBundleDto> LoadAiReviewJobsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var total = await context.AiReviewJobs.AsNoTracking().CountAsync(ct);
        var byStatus = await EnumCountsAsync(context.AiReviewJobs.AsNoTracking(), j => j.Status, ct);
        return Merge(meta, snapshot, total, byStatus: byStatus);
    }

    private async Task<OperatorAiEntityBundleDto> LoadUserFaceModerationsAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var q = context.UserFaceModerations.AsNoTracking();
        var total = await q.CountAsync(ct);
        var active = await q.CountAsync(m => m.LiftedAt == null, ct);
        var series = await BuildTimeseriesAsync(q, m => m.BannedAt, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["activeBans"] = active,
                ["lifted"] = total - active,
            },
            timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadOAuthRefreshTokensAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = context.OAuthRefreshTokens.AsNoTracking();
        var total = await q.CountAsync(ct);
        var revoked = await q.CountAsync(t => t.RevokedAtUtc != null, ct);
        var expired = await q.CountAsync(t => t.RevokedAtUtc == null && t.ExpiresAtUtc <= now, ct);
        var active = total - revoked - expired;
        var series = await BuildTimeseriesAsync(q, t => t.CreatedAtUtc, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["active"] = active,
                ["revoked"] = revoked,
                ["expired"] = expired,
            },
            timeseries: series);
    }

    private async Task<OperatorAiEntityBundleDto> LoadOperatorAiMessagesAsync(
        ApplicationDbContext context,
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        CancellationToken ct)
    {
        var q = context.OperatorAiMessages.AsNoTracking();
        var total = await q.CountAsync(ct);
        var userCount = await q.CountAsync(m => m.Role == OperatorAiMessage.RoleUser, ct);
        var assistantCount = await q.CountAsync(m => m.Role == OperatorAiMessage.RoleAssistant, ct);
        var series = await BuildTimeseriesAsync(q, m => m.CreatedAt, ct);
        return Merge(meta, snapshot, total,
            byStatus: new Dictionary<string, int>
            {
                ["user"] = userCount,
                ["assistant"] = assistantCount,
            },
            timeseries: series);
    }

    private async Task<IReadOnlyList<OperatorAiEntityBundleTimeseriesBucketDto>> BuildTimeseriesAsync<T>(
        IQueryable<T> query,
        Expression<Func<T, DateTime>> timestamp,
        CancellationToken ct)
    {
        var days = _options.LiveTimeseriesDays;
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.Date.AddDays(-(days - 1));
        var timestamps = await query
            .Where(BuildRangePredicate(timestamp, fromUtc, toUtc))
            .Select(timestamp)
            .ToListAsync(ct);
        return StatsTimeseriesBucketing
            .BucketizeUtc(timestamps, fromUtc, toUtc, "day")
            .Select(b => new OperatorAiEntityBundleTimeseriesBucketDto
            {
                PeriodStartUtc = b.PeriodStartUtc,
                Count = b.Count,
            })
            .ToList();
    }

    private static Expression<Func<T, bool>> BuildRangePredicate<T>(
        Expression<Func<T, DateTime>> field,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var body = Expression.AndAlso(
            Expression.GreaterThanOrEqual(field.Body, Expression.Constant(fromUtc)),
            Expression.LessThanOrEqual(field.Body, Expression.Constant(toUtc)));
        return Expression.Lambda<Func<T, bool>>(body, field.Parameters);
    }

    private static async Task<Dictionary<string, int>> EnumCountsAsync<T, TEnum>(
        IQueryable<T> query,
        Expression<Func<T, TEnum>> selector,
        CancellationToken ct) where TEnum : struct, Enum
    {
        var groups = await query
            .GroupBy(selector)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var dict = groups.ToDictionary(g => g.Key.ToString()!, g => g.Count, StringComparer.Ordinal);
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (!dict.ContainsKey(name))
                dict[name] = 0;
        }

        return dict;
    }

    private static OperatorAiEntityBundleDto Merge(
        OperatorAiBundleCatalogEntryDto meta,
        DateTime snapshot,
        int total,
        Dictionary<string, int>? byStatus = null,
        Dictionary<string, int>? byAiReviewStatus = null,
        Dictionary<string, int>? byType = null,
        IReadOnlyList<OperatorAiEntityBundleTimeseriesBucketDto>? timeseries = null) =>
        new()
        {
            BundleId = meta.Id,
            Entity = meta.EntityName,
            Index = meta.Index,
            SnapshotUtc = snapshot,
            TotalCount = total,
            ByStatus = byStatus,
            ByAiReviewStatus = byAiReviewStatus,
            ByType = byType,
            TimeseriesLast7Days = timeseries,
        };
}
