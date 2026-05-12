using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Redacts internal AI-only fields on rejected/removed content after a cooling period.
/// </summary>
public interface IContentRetentionCleanupService
{
    Task<RetentionCleanupResult> RunAsync(bool dryRun, DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed record RetentionCleanupResult(int BlogsRedacted, int AlbumsRedacted, int ReelsRedacted);

/// <summary>
/// Policy: after <see cref="ContentModerationHelpers.DefaultRetentionDays"/> from human/removal decision,
/// redact internal AI trace fields while preserving audit events and creator-safe messages.
/// <para>
/// "Redacted" rows are still counted in <see cref="RetentionCleanupResult"/> even in dry-run mode so operators can validate scope before enabling <c>Retention:Execute</c>.
/// </para>
/// </summary>
public sealed class ContentRetentionCleanupService : IContentRetentionCleanupService
{
    private readonly ApplicationDbContext _context;

    public ContentRetentionCleanupService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RetentionCleanupResult> RunAsync(bool dryRun, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Items older than this instant are eligible for AI-field redaction.
        var cutoff = nowUtc.AddDays(-ContentModerationHelpers.DefaultRetentionDays);
        var blogsRedacted = 0;
        var albumsRedacted = 0;
        var reelsRedacted = 0;

        // Blogs: terminal moderation states only; use human review time (or removal time) as the retention anchor.
        var blogs = await _context.Blogs
            .Where(b => b.ApprovalStatus == ContentApprovalStatus.Rejected || b.ApprovalStatus == ContentApprovalStatus.Removed)
            .Where(b => (b.ApprovalStatus == ContentApprovalStatus.Removed
                ? b.RemovedAtUtc ?? b.HumanReviewedAtUtc
                : b.HumanReviewedAtUtc) != null)
            .Where(b =>
                (b.ApprovalStatus == ContentApprovalStatus.Removed
                    ? b.RemovedAtUtc ?? b.HumanReviewedAtUtc!.Value
                    : b.HumanReviewedAtUtc!.Value) <= cutoff)
            .Where(b => b.AiReviewReason != null || b.AiReviewTraceId != null || b.AiReviewModelVersion != null)
            .ToListAsync(cancellationToken);

        foreach (var blog in blogs)
        {
            blogsRedacted++;
            if (dryRun)
                continue;
            blog.AiReviewReason = null;
            blog.AiReviewTraceId = null;
            blog.AiReviewModelVersion = null;
            _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
                ModeratedContentType.Blog,
                blog.Id,
                blog.FaceId,
                blog.ApprovalStatus,
                blog.ApprovalStatus,
                blog.AiReviewStatus,
                blog.AiReviewStatus,
                ModerationActorType.Retention,
                null,
                "Automated retention: internal AI trace fields redacted.",
                null,
                null,
                null));
        }

        // Albums: same eligibility rules as blogs; include AlbumFaces so we can stamp a face id on audit events.
        var albums = await _context.Albums
            .Where(a => a.ApprovalStatus == ContentApprovalStatus.Rejected || a.ApprovalStatus == ContentApprovalStatus.Removed)
            .Where(a => (a.ApprovalStatus == ContentApprovalStatus.Removed
                ? a.RemovedAtUtc ?? a.HumanReviewedAtUtc
                : a.HumanReviewedAtUtc) != null)
            .Where(a =>
                (a.ApprovalStatus == ContentApprovalStatus.Removed
                    ? a.RemovedAtUtc ?? a.HumanReviewedAtUtc!.Value
                    : a.HumanReviewedAtUtc!.Value) <= cutoff)
            .Where(a => a.AiReviewReason != null || a.AiReviewTraceId != null || a.AiReviewModelVersion != null)
            .Include(a => a.AlbumFaces)
            .ToListAsync(cancellationToken);

        foreach (var album in albums)
        {
            albumsRedacted++;
            if (dryRun)
                continue;
            album.AiReviewReason = null;
            album.AiReviewTraceId = null;
            album.AiReviewModelVersion = null;
            var faceId = album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault();
            _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
                ModeratedContentType.Album,
                album.Id,
                faceId,
                album.ApprovalStatus,
                album.ApprovalStatus,
                album.AiReviewStatus,
                album.AiReviewStatus,
                ModerationActorType.Retention,
                null,
                "Automated retention: internal AI trace fields redacted.",
                null,
                null,
                null));
        }

        // Reels: multi-face join table analogous to albums.
        var reels = await _context.Reels
            .Where(r => r.ApprovalStatus == ContentApprovalStatus.Rejected || r.ApprovalStatus == ContentApprovalStatus.Removed)
            .Where(r => (r.ApprovalStatus == ContentApprovalStatus.Removed
                ? r.RemovedAtUtc ?? r.HumanReviewedAtUtc
                : r.HumanReviewedAtUtc) != null)
            .Where(r =>
                (r.ApprovalStatus == ContentApprovalStatus.Removed
                    ? r.RemovedAtUtc ?? r.HumanReviewedAtUtc!.Value
                    : r.HumanReviewedAtUtc!.Value) <= cutoff)
            .Where(r => r.AiReviewReason != null || r.AiReviewTraceId != null || r.AiReviewModelVersion != null)
            .Include(r => r.ReelFaces)
            .ToListAsync(cancellationToken);

        foreach (var reel in reels)
        {
            reelsRedacted++;
            if (dryRun)
                continue;
            reel.AiReviewReason = null;
            reel.AiReviewTraceId = null;
            reel.AiReviewModelVersion = null;
            var faceId = reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault();
            _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
                ModeratedContentType.Reel,
                reel.Id,
                faceId,
                reel.ApprovalStatus,
                reel.ApprovalStatus,
                reel.AiReviewStatus,
                reel.AiReviewStatus,
                ModerationActorType.Retention,
                null,
                "Automated retention: internal AI trace fields redacted.",
                null,
                null,
                null));
        }

        // Persist redactions and audit rows in one transaction; dry-run skips SaveChanges entirely.
        if (!dryRun && (blogsRedacted > 0 || albumsRedacted > 0 || reelsRedacted > 0))
            await _context.SaveChangesAsync(cancellationToken);

        return new RetentionCleanupResult(blogsRedacted, albumsRedacted, reelsRedacted);
    }
}
