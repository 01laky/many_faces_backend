using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Moderation;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Super-admin moderation API: unified queue across albums/blogs/reels, audit history, operational metrics with alerts,
/// single-item decisions, and bulk actions (including AI requeue). All mutating endpoints require global super-admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ContentModerationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAccessEvaluator _access;
    private readonly IContentModerationMetrics _metrics;
    private readonly IRedisJobQueue _jobQueue;
    private readonly ILogger<ContentModerationController> _logger;
    private readonly IContentModerationNotifier _moderationNotifier;

    public ContentModerationController(
        ApplicationDbContext context,
        IAccessEvaluator access,
        IContentModerationMetrics metrics,
        IRedisJobQueue jobQueue,
        ILogger<ContentModerationController> logger,
        IContentModerationNotifier moderationNotifier)
    {
        _context = context;
        _access = access;
        _metrics = metrics;
        _jobQueue = jobQueue;
        _logger = logger;
        _moderationNotifier = moderationNotifier;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private bool CanModerate() => _access.IsGlobalSuperAdmin(User);

    /// <summary>
    /// Returns the moderation queue with optional filters. Query parameters mirror the admin UI:
    /// content type, approval/AI status, face, author, risk, moderation version, substring flag search,
    /// confidence band, submission time window, human reviewer id, and minimum queue age (hours since submit).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueue([FromQuery] GetModerationQueueQuery q)
    {
        if (!CanModerate())
            return Forbid();

        var items = new List<ModerationItemDto>();

        if (q.ContentType is null or ModeratedContentType.Album)
        {
            var albumRows = await _context.Albums
                .Include(a => a.Creator)
                .Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
                .Where(a => q.ApprovalStatus == null || a.ApprovalStatus == q.ApprovalStatus)
                .Where(a => q.AiReviewStatus == null || a.AiReviewStatus == q.AiReviewStatus)
                .Where(a => q.FaceId == null || a.AlbumFaces.Any(af => af.FaceId == q.FaceId))
                .Where(a => string.IsNullOrWhiteSpace(q.AuthorId) || a.CreatorId == q.AuthorId)
                .Where(a => q.RiskLevel == null || a.AiReviewRiskLevel == q.RiskLevel)
                .Where(a => q.ModerationVersion == null || a.ModerationVersion == q.ModerationVersion)
                .Where(a => string.IsNullOrWhiteSpace(q.FlagContains) ||
                    (a.AiReviewFlagsJson != null && a.AiReviewFlagsJson.ToLower().Contains(q.FlagContains.ToLower())))
                .Where(a => !q.MinConfidence.HasValue || (a.AiReviewConfidence != null && a.AiReviewConfidence >= q.MinConfidence))
                .Where(a => !q.MaxConfidence.HasValue || (a.AiReviewConfidence != null && a.AiReviewConfidence <= q.MaxConfidence))
                .Where(a => !q.SubmittedFromUtc.HasValue || (a.SubmittedAtUtc != null && a.SubmittedAtUtc >= q.SubmittedFromUtc))
                .Where(a => !q.SubmittedToUtc.HasValue || (a.SubmittedAtUtc != null && a.SubmittedAtUtc <= q.SubmittedToUtc))
                .Where(a => string.IsNullOrWhiteSpace(q.ReviewedByUserId) || a.HumanReviewedByUserId == q.ReviewedByUserId)
                .Where(a => !q.MinQueueAgeHours.HasValue ||
                    (a.SubmittedAtUtc != null && a.SubmittedAtUtc <= DateTime.UtcNow.AddHours(-q.MinQueueAgeHours.Value)))
                .Select(a => new
                {
                    Entity = a,
                    FaceId = a.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
                    FaceTitle = a.AlbumFaces.Select(af => af.Face.Title).FirstOrDefault() ?? string.Empty,
                })
                .ToListAsync();
            items.AddRange(albumRows.Select(row => MapAlbum(row.Entity, row.FaceId, row.FaceTitle)));
        }

        if (q.ContentType is null or ModeratedContentType.Blog)
        {
            var blogs = await _context.Blogs
                .Include(b => b.Creator)
                .Include(b => b.Face)
                .Where(b => q.ApprovalStatus == null || b.ApprovalStatus == q.ApprovalStatus)
                .Where(b => q.AiReviewStatus == null || b.AiReviewStatus == q.AiReviewStatus)
                .Where(b => q.FaceId == null || b.FaceId == q.FaceId)
                .Where(b => string.IsNullOrWhiteSpace(q.AuthorId) || b.CreatorId == q.AuthorId)
                .Where(b => q.RiskLevel == null || b.AiReviewRiskLevel == q.RiskLevel)
                .Where(b => q.ModerationVersion == null || b.ModerationVersion == q.ModerationVersion)
                .Where(b => string.IsNullOrWhiteSpace(q.FlagContains) ||
                    (b.AiReviewFlagsJson != null && b.AiReviewFlagsJson.ToLower().Contains(q.FlagContains.ToLower())))
                .Where(b => !q.MinConfidence.HasValue || (b.AiReviewConfidence != null && b.AiReviewConfidence >= q.MinConfidence))
                .Where(b => !q.MaxConfidence.HasValue || (b.AiReviewConfidence != null && b.AiReviewConfidence <= q.MaxConfidence))
                .Where(b => !q.SubmittedFromUtc.HasValue || (b.SubmittedAtUtc != null && b.SubmittedAtUtc >= q.SubmittedFromUtc))
                .Where(b => !q.SubmittedToUtc.HasValue || (b.SubmittedAtUtc != null && b.SubmittedAtUtc <= q.SubmittedToUtc))
                .Where(b => string.IsNullOrWhiteSpace(q.ReviewedByUserId) || b.HumanReviewedByUserId == q.ReviewedByUserId)
                .Where(b => !q.MinQueueAgeHours.HasValue ||
                    (b.SubmittedAtUtc != null && b.SubmittedAtUtc <= DateTime.UtcNow.AddHours(-q.MinQueueAgeHours.Value)))
                .ToListAsync();
            items.AddRange(blogs.Select(MapBlog));
        }

        if (q.ContentType is null or ModeratedContentType.Reel)
        {
            var reelRows = await _context.Reels
                .Include(r => r.Creator)
                .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
                .Where(r => q.ApprovalStatus == null || r.ApprovalStatus == q.ApprovalStatus)
                .Where(r => q.AiReviewStatus == null || r.AiReviewStatus == q.AiReviewStatus)
                .Where(r => q.FaceId == null || r.ReelFaces.Any(rf => rf.FaceId == q.FaceId))
                .Where(r => string.IsNullOrWhiteSpace(q.AuthorId) || r.CreatorId == q.AuthorId)
                .Where(r => q.RiskLevel == null || r.AiReviewRiskLevel == q.RiskLevel)
                .Where(r => q.ModerationVersion == null || r.ModerationVersion == q.ModerationVersion)
                .Where(r => string.IsNullOrWhiteSpace(q.FlagContains) ||
                    (r.AiReviewFlagsJson != null && r.AiReviewFlagsJson.ToLower().Contains(q.FlagContains.ToLower())))
                .Where(r => !q.MinConfidence.HasValue || (r.AiReviewConfidence != null && r.AiReviewConfidence >= q.MinConfidence))
                .Where(r => !q.MaxConfidence.HasValue || (r.AiReviewConfidence != null && r.AiReviewConfidence <= q.MaxConfidence))
                .Where(r => !q.SubmittedFromUtc.HasValue || (r.SubmittedAtUtc != null && r.SubmittedAtUtc >= q.SubmittedFromUtc))
                .Where(r => !q.SubmittedToUtc.HasValue || (r.SubmittedAtUtc != null && r.SubmittedAtUtc <= q.SubmittedToUtc))
                .Where(r => string.IsNullOrWhiteSpace(q.ReviewedByUserId) || r.HumanReviewedByUserId == q.ReviewedByUserId)
                .Where(r => !q.MinQueueAgeHours.HasValue ||
                    (r.SubmittedAtUtc != null && r.SubmittedAtUtc <= DateTime.UtcNow.AddHours(-q.MinQueueAgeHours.Value)))
                .Select(r => new
                {
                    Entity = r,
                    FaceId = r.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
                    FaceTitle = r.ReelFaces.Select(rf => rf.Face.Title).FirstOrDefault() ?? string.Empty,
                })
                .ToListAsync();
            items.AddRange(reelRows.Select(row => MapReel(row.Entity, row.FaceId, row.FaceTitle)));
        }

        return Ok(items.OrderByDescending(i => i.SubmittedAtUtc ?? i.CreatedAt));
    }

    /// <summary>Maps album entity to queue DTO with PI-8 plain-text preview fields (no raw HTML).</summary>
    private static ModerationItemDto MapAlbum(Album album, int faceId, string faceTitle) =>
        new(
            ModeratedContentType.Album,
            album.Id,
            album.Title,
            faceId,
            faceTitle,
            album.CreatorId,
            CreatorDisplayName(album.Creator),
            album.ApprovalStatus,
            album.AiReviewStatus,
            album.AiReviewDecision,
            album.AiReviewConfidence,
            album.AiReviewRiskLevel,
            album.AiReviewFlagsJson,
            album.AiReviewReason,
            album.AiReviewUserMessage,
            album.AiReviewModelVersion,
            album.AiReviewTraceId,
            album.SubmittedAtUtc,
            album.HumanReviewedAtUtc,
            album.HumanDecisionReason,
            album.RemovedAtUtc,
            album.RemovalReason,
            album.CreatedAt,
            ContentModerationPreviewText.ToPlainTextPreview(album.Description),
            null);

    private static ModerationItemDto MapBlog(Blog blog) =>
        new(
            ModeratedContentType.Blog,
            blog.Id,
            blog.Title,
            blog.FaceId,
            blog.Face.Title,
            blog.CreatorId,
            CreatorDisplayName(blog.Creator),
            blog.ApprovalStatus,
            blog.AiReviewStatus,
            blog.AiReviewDecision,
            blog.AiReviewConfidence,
            blog.AiReviewRiskLevel,
            blog.AiReviewFlagsJson,
            blog.AiReviewReason,
            blog.AiReviewUserMessage,
            blog.AiReviewModelVersion,
            blog.AiReviewTraceId,
            blog.SubmittedAtUtc,
            blog.HumanReviewedAtUtc,
            blog.HumanDecisionReason,
            blog.RemovedAtUtc,
            blog.RemovalReason,
            blog.CreatedAt,
            ContentModerationPreviewText.ToPlainTextPreview(blog.Content),
            null);

    private static ModerationItemDto MapReel(Reel reel, int faceId, string faceTitle) =>
        new(
            ModeratedContentType.Reel,
            reel.Id,
            reel.Title,
            faceId,
            faceTitle,
            reel.CreatorId,
            CreatorDisplayName(reel.Creator),
            reel.ApprovalStatus,
            reel.AiReviewStatus,
            reel.AiReviewDecision,
            reel.AiReviewConfidence,
            reel.AiReviewRiskLevel,
            reel.AiReviewFlagsJson,
            reel.AiReviewReason,
            reel.AiReviewUserMessage,
            reel.AiReviewModelVersion,
            reel.AiReviewTraceId,
            reel.SubmittedAtUtc,
            reel.HumanReviewedAtUtc,
            reel.HumanDecisionReason,
            reel.RemovedAtUtc,
            reel.RemovalReason,
            reel.CreatedAt,
            ContentModerationPreviewText.ToPlainTextPreview(reel.Description),
            ContentModerationPreviewText.ToMediaUrlPreview(reel.VideoUrl));

    private static string CreatorDisplayName(ApplicationUser creator) =>
        $"{creator.FirstName ?? ""} {creator.LastName ?? ""}".Trim();

    /// <summary>Immutable audit trail for a single moderated entity (newest first).</summary>
    [HttpGet("{contentType}/{contentId:int}/events")]
    public async Task<IActionResult> GetEvents(ModeratedContentType contentType, int contentId)
    {
        if (!CanModerate())
            return Forbid();

        var events = await _context.ContentModerationEvents
            .Where(e => e.ContentType == contentType && e.ContentId == contentId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// Returns <see cref="ModerationMetricsWithAlerts"/> JSON: numeric snapshot plus derived alerts.
    /// Each alert is also written as a structured warning log line for external log aggregation.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        if (!CanModerate())
            return Forbid();

        var metrics = await _metrics.GetSnapshotAsync();
        var alerts = ContentModerationAlertEvaluator.Evaluate(metrics, DateTime.UtcNow);
        foreach (var alert in alerts)
        {
            _logger.LogWarning(
                "ModerationOpsAlert {AlertCode} {Severity} {Message}",
                alert.Code,
                alert.Severity,
                alert.Message);
        }

        return Ok(new ModerationMetricsWithAlerts(metrics, alerts));
    }

    /// <summary>
    /// Applies approve/reject/remove or AI requeue to many items in one request. Per-item results preserve partial success visibility.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkModerate([FromBody] BulkModerationRequest request)
    {
        if (!CanModerate())
            return Forbid();
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();
        var results = new List<BulkModerationResultDto>();
        foreach (var item in request.Items.DistinctBy(i => (i.ContentType, i.ContentId)))
        {
            var actionResult = request.Action == BulkModerationAction.RequeueAiReview
                ? await RequeueAiReviewAsync(item.ContentType, item.ContentId)
                : await ApplyDecisionCoreAsync(
                    item.ContentType,
                    item.ContentId,
                    request.Action switch
                    {
                        BulkModerationAction.Approve => ContentApprovalStatus.Approved,
                        BulkModerationAction.Reject => ContentApprovalStatus.Rejected,
                        BulkModerationAction.Remove => ContentApprovalStatus.Removed,
                        _ => ContentApprovalStatus.PendingApproval,
                    },
                    new ModerationDecisionDto(request.Reason, request.UserMessage),
                    saveChanges: false);
            results.Add(new BulkModerationResultDto(
                item.ContentType,
                item.ContentId,
                actionResult.Success,
                actionResult.StatusCode,
                actionResult.Message,
                actionResult.ApprovalStatus?.ToString(),
                actionResult.AiReviewStatus?.ToString()));
        }

        await _context.SaveChangesAsync();
        return Ok(new BulkModerationResponse(results));
    }

    [HttpPost("{contentType}/{contentId:int}/approve")]
    public Task<IActionResult> Approve(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto? decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Approved, decision);

    [HttpPost("{contentType}/{contentId:int}/reject")]
    public Task<IActionResult> Reject(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Rejected, decision);

    [HttpPost("{contentType}/{contentId:int}/remove")]
    public Task<IActionResult> Remove(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Removed, decision);

    private async Task<IActionResult> ApplyDecisionAsync(
        ModeratedContentType contentType,
        int contentId,
        ContentApprovalStatus targetStatus,
        ModerationDecisionDto? decision)
    {
        var result = await ApplyDecisionCoreAsync(contentType, contentId, targetStatus, decision, saveChanges: true);
        return result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(new
            {
                approvalStatus = result.ApprovalStatus?.ToString(),
                aiReviewStatus = result.AiReviewStatus?.ToString(),
            }),
            StatusCodes.Status400BadRequest => BadRequest(new { error = result.Message }),
            StatusCodes.Status401Unauthorized => Unauthorized(),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { error = result.Message }),
            _ => StatusCode(result.StatusCode, new { error = result.Message }),
        };
    }

    private async Task<ModerationActionResult> ApplyDecisionCoreAsync(
        ModeratedContentType contentType,
        int contentId,
        ContentApprovalStatus targetStatus,
        ModerationDecisionDto? decision,
        bool saveChanges)
    {
        if (!CanModerate())
            return ModerationActionResult.Fail(StatusCodes.Status403Forbidden, "Forbidden");
        if (string.IsNullOrEmpty(UserId))
            return ModerationActionResult.Fail(StatusCodes.Status401Unauthorized, "Unauthorized");
        if (targetStatus is ContentApprovalStatus.Rejected or ContentApprovalStatus.Removed &&
            string.IsNullOrWhiteSpace(decision?.Reason))
            return ModerationActionResult.Fail(StatusCodes.Status400BadRequest, "Reason is required");

        var item = await LoadModeratedItemAsync(contentType, contentId);
        if (item == null)
            return ModerationActionResult.Fail(StatusCodes.Status404NotFound, "Content not found");

        if (item.ApprovalStatus == targetStatus)
            return ModerationActionResult.Ok(item.ApprovalStatus, item.AiReviewStatus, "Already in target status");

        if (targetStatus == ContentApprovalStatus.Approved &&
            item.AiReviewStatus == AiReviewStatus.RecommendedReject &&
            string.IsNullOrWhiteSpace(decision?.Reason))
            return ModerationActionResult.Fail(StatusCodes.Status400BadRequest, "Override reason is required when approving AI-recommended rejection");

        var oldApproval = item.ApprovalStatus;
        var oldAiStatus = item.AiReviewStatus;
        item.ApprovalStatus = targetStatus;
        item.HumanReviewedAtUtc = DateTime.UtcNow;
        item.HumanReviewedByUserId = UserId;
        item.HumanDecisionReason = decision?.Reason?.Trim();

        if (targetStatus == ContentApprovalStatus.Approved)
        {
            item.RemovedAtUtc = null;
            item.RemovedByUserId = null;
            item.RemovalReason = null;
        }
        else if (targetStatus == ContentApprovalStatus.Removed)
        {
            item.RemovedAtUtc = DateTime.UtcNow;
            item.RemovedByUserId = UserId;
            item.RemovalReason = decision?.Reason?.Trim();
        }

        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            contentType,
            contentId,
            item.FaceId,
            oldApproval,
            targetStatus,
            oldAiStatus,
            item.AiReviewStatus,
            ModerationActorType.SuperAdmin,
            UserId,
            decision?.Reason,
            decision?.UserMessage,
            item.AiReviewTraceId,
            item.AiReviewModelVersion));

        await AddCreatorNotificationAsync(item.CreatorId, targetStatus, decision?.UserMessage ?? decision?.Reason);

        if (saveChanges)
            await _context.SaveChangesAsync();
        return ModerationActionResult.Ok(item.ApprovalStatus, item.AiReviewStatus, "Updated");
    }

    private async Task<ModerationActionResult> RequeueAiReviewAsync(ModeratedContentType contentType, int contentId)
    {
        var item = await LoadModeratedItemAsync(contentType, contentId);
        if (item == null)
            return ModerationActionResult.Fail(StatusCodes.Status404NotFound, "Content not found");
        if (item.ApprovalStatus != ContentApprovalStatus.PendingApproval)
            return ModerationActionResult.Fail(StatusCodes.Status409Conflict, "Only pending content can be requeued");

        var oldAiStatus = item.AiReviewStatus;
        item.AiReviewStatus = AiReviewStatus.Queued;
        var job = await _context.AiReviewJobs
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(j =>
                j.ContentType == contentType &&
                j.ContentId == contentId &&
                j.ModerationVersion == item.ModerationVersion);
        if (job == null)
        {
            job = new AiReviewJob
            {
                ContentType = contentType,
                ContentId = contentId,
                FaceId = item.FaceId,
                CreatedByUserId = item.CreatorId,
                ModerationVersion = item.ModerationVersion,
                MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _context.AiReviewJobs.Add(job);
        }
        job.Status = AiReviewJobStatus.Queued;
        job.NextAttemptAtUtc = null;
        job.StartedAtUtc = null;
        job.CompletedAtUtc = null;
        job.LastError = null;
        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            contentType,
            contentId,
            item.FaceId,
            item.ApprovalStatus,
            item.ApprovalStatus,
            oldAiStatus,
            AiReviewStatus.Queued,
            ModerationActorType.SuperAdmin,
            UserId,
            "AI review requeued by superadmin.",
            null,
            item.AiReviewTraceId,
            item.AiReviewModelVersion));
        await _jobQueue.EnqueueAsync(
            ContentModerationHelpers.AiReviewJobType,
            ContentModerationHelpers.BuildAiReviewPayload(contentType, contentId, item.ModerationVersion));
        return ModerationActionResult.Ok(item.ApprovalStatus, item.AiReviewStatus, "Requeued");
    }

    private Task AddCreatorNotificationAsync(string creatorId, ContentApprovalStatus targetStatus, string? reason)
    {
        var title = targetStatus switch
        {
            ContentApprovalStatus.Approved => "Content approved",
            ContentApprovalStatus.Rejected => "Content rejected",
            ContentApprovalStatus.Removed => "Content removed",
            _ => "Content moderation updated",
        };
        var message = targetStatus switch
        {
            ContentApprovalStatus.Approved => "Your submitted content was approved.",
            ContentApprovalStatus.Rejected => string.IsNullOrWhiteSpace(reason) ? "Your submitted content was rejected." : $"Your submitted content was rejected: {ContentModerationHelpers.RedactForAudit(reason)}",
            ContentApprovalStatus.Removed => string.IsNullOrWhiteSpace(reason) ? "Your content was removed." : $"Your content was removed: {ContentModerationHelpers.RedactForAudit(reason)}",
            _ => "Your submitted content moderation status changed.",
        };
        _moderationNotifier.NotifyCreator(creatorId, title, message, "content_moderation");
        return Task.CompletedTask;
    }

    private async Task<ModeratedItemAdapter?> LoadModeratedItemAsync(ModeratedContentType contentType, int contentId)
    {
        switch (contentType)
        {
            case ModeratedContentType.Album:
                {
                    var album = await _context.Albums
                        .Include(a => a.AlbumFaces)
                        .FirstOrDefaultAsync(a => a.Id == contentId);
                    return album == null
                        ? null
                        : new ModeratedItemAdapter(
                            album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
                            album.CreatorId,
                            () => album.ApprovalStatus,
                            value => album.ApprovalStatus = value,
                            () => album.AiReviewStatus,
                            value => album.AiReviewStatus = value,
                            () => album.ModerationVersion,
                            () => album.AiReviewTraceId,
                            () => album.AiReviewModelVersion,
                            value => album.HumanReviewedAtUtc = value,
                            value => album.HumanReviewedByUserId = value,
                            value => album.HumanDecisionReason = value,
                            value => album.RemovedAtUtc = value,
                            value => album.RemovedByUserId = value,
                            value => album.RemovalReason = value);
                }
            case ModeratedContentType.Blog:
                {
                    var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.Id == contentId);
                    return blog == null
                        ? null
                        : new ModeratedItemAdapter(
                            blog.FaceId,
                            blog.CreatorId,
                            () => blog.ApprovalStatus,
                            value => blog.ApprovalStatus = value,
                            () => blog.AiReviewStatus,
                            value => blog.AiReviewStatus = value,
                            () => blog.ModerationVersion,
                            () => blog.AiReviewTraceId,
                            () => blog.AiReviewModelVersion,
                            value => blog.HumanReviewedAtUtc = value,
                            value => blog.HumanReviewedByUserId = value,
                            value => blog.HumanDecisionReason = value,
                            value => blog.RemovedAtUtc = value,
                            value => blog.RemovedByUserId = value,
                            value => blog.RemovalReason = value);
                }
            case ModeratedContentType.Reel:
                {
                    var reel = await _context.Reels
                        .Include(r => r.ReelFaces)
                        .FirstOrDefaultAsync(r => r.Id == contentId);
                    return reel == null
                        ? null
                        : new ModeratedItemAdapter(
                            reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
                            reel.CreatorId,
                            () => reel.ApprovalStatus,
                            value => reel.ApprovalStatus = value,
                            () => reel.AiReviewStatus,
                            value => reel.AiReviewStatus = value,
                            () => reel.ModerationVersion,
                            () => reel.AiReviewTraceId,
                            () => reel.AiReviewModelVersion,
                            value => reel.HumanReviewedAtUtc = value,
                            value => reel.HumanReviewedByUserId = value,
                            value => reel.HumanDecisionReason = value,
                            value => reel.RemovedAtUtc = value,
                            value => reel.RemovedByUserId = value,
                            value => reel.RemovalReason = value);
                }
            default:
                return null;
        }
    }

    private sealed class ModeratedItemAdapter
    {
        private readonly Func<ContentApprovalStatus> _getApprovalStatus;
        private readonly Action<ContentApprovalStatus> _setApprovalStatus;
        private readonly Func<AiReviewStatus> _getAiReviewStatus;
        private readonly Action<AiReviewStatus> _setAiReviewStatus;
        private readonly Func<int> _getModerationVersion;
        private readonly Func<string?> _getAiTraceId;
        private readonly Func<string?> _getAiModelVersion;
        private readonly Action<DateTime?> _setHumanReviewedAtUtc;
        private readonly Action<string?> _setHumanReviewedByUserId;
        private readonly Action<string?> _setHumanDecisionReason;
        private readonly Action<DateTime?> _setRemovedAtUtc;
        private readonly Action<string?> _setRemovedByUserId;
        private readonly Action<string?> _setRemovalReason;

        public ModeratedItemAdapter(
            int faceId,
            string creatorId,
            Func<ContentApprovalStatus> getApprovalStatus,
            Action<ContentApprovalStatus> setApprovalStatus,
            Func<AiReviewStatus> getAiReviewStatus,
            Action<AiReviewStatus> setAiReviewStatus,
            Func<int> getModerationVersion,
            Func<string?> getAiTraceId,
            Func<string?> getAiModelVersion,
            Action<DateTime?> setHumanReviewedAtUtc,
            Action<string?> setHumanReviewedByUserId,
            Action<string?> setHumanDecisionReason,
            Action<DateTime?> setRemovedAtUtc,
            Action<string?> setRemovedByUserId,
            Action<string?> setRemovalReason)
        {
            FaceId = faceId;
            CreatorId = creatorId;
            _getApprovalStatus = getApprovalStatus;
            _setApprovalStatus = setApprovalStatus;
            _getAiReviewStatus = getAiReviewStatus;
            _setAiReviewStatus = setAiReviewStatus;
            _getModerationVersion = getModerationVersion;
            _getAiTraceId = getAiTraceId;
            _getAiModelVersion = getAiModelVersion;
            _setHumanReviewedAtUtc = setHumanReviewedAtUtc;
            _setHumanReviewedByUserId = setHumanReviewedByUserId;
            _setHumanDecisionReason = setHumanDecisionReason;
            _setRemovedAtUtc = setRemovedAtUtc;
            _setRemovedByUserId = setRemovedByUserId;
            _setRemovalReason = setRemovalReason;
        }

        public int FaceId { get; }
        public string CreatorId { get; }

        public ContentApprovalStatus ApprovalStatus
        {
            get => _getApprovalStatus();
            set => _setApprovalStatus(value);
        }

        public AiReviewStatus AiReviewStatus
        {
            get => _getAiReviewStatus();
            set => _setAiReviewStatus(value);
        }

        public int ModerationVersion => _getModerationVersion();

        public string? AiReviewTraceId => _getAiTraceId();

        public string? AiReviewModelVersion => _getAiModelVersion();

        public DateTime? HumanReviewedAtUtc
        {
            set => _setHumanReviewedAtUtc(value);
        }

        public string? HumanReviewedByUserId
        {
            set => _setHumanReviewedByUserId(value);
        }

        public string? HumanDecisionReason
        {
            set => _setHumanDecisionReason(value);
        }

        public DateTime? RemovedAtUtc
        {
            set => _setRemovedAtUtc(value);
        }

        public string? RemovedByUserId
        {
            set => _setRemovedByUserId(value);
        }

        public string? RemovalReason
        {
            set => _setRemovalReason(value);
        }

        public object ToResponse() => new
        {
            approvalStatus = ApprovalStatus.ToString(),
            aiReviewStatus = AiReviewStatus.ToString(),
        };
    }
}

public sealed record BulkModerationResponse(List<BulkModerationResultDto> Results);

public sealed record BulkModerationResultDto(
    ModeratedContentType ContentType,
    int ContentId,
    bool Success,
    int StatusCode,
    string Message,
    string? ApprovalStatus,
    string? AiReviewStatus);

internal sealed record ModerationActionResult(
    bool Success,
    int StatusCode,
    string Message,
    ContentApprovalStatus? ApprovalStatus,
    AiReviewStatus? AiReviewStatus)
{
    public static ModerationActionResult Ok(ContentApprovalStatus approvalStatus, AiReviewStatus aiReviewStatus, string message) =>
        new(true, StatusCodes.Status200OK, message, approvalStatus, aiReviewStatus);

    public static ModerationActionResult Fail(int statusCode, string message) =>
        new(false, statusCode, message, null, null);
}

/// <param name="BodyPreviewPlainText">SHV2 PI-8: stripped HTML / plain description for operator preview (never raw HTML).</param>
/// <param name="MediaUrlPreview">Optional reel media URL preview (plain string).</param>
public sealed record ModerationItemDto(
    ModeratedContentType ContentType,
    int ContentId,
    string Title,
    int FaceId,
    string FaceTitle,
    string CreatorId,
    string CreatorName,
    ContentApprovalStatus ApprovalStatus,
    AiReviewStatus AiReviewStatus,
    AiReviewDecision AiReviewDecision,
    double? AiReviewConfidence,
    AiReviewRiskLevel AiReviewRiskLevel,
    string? AiReviewFlagsJson,
    string? AiReviewReason,
    string? AiReviewUserMessage,
    string? AiReviewModelVersion,
    string? AiReviewTraceId,
    DateTime? SubmittedAtUtc,
    DateTime? HumanReviewedAtUtc,
    string? HumanDecisionReason,
    DateTime? RemovedAtUtc,
    string? RemovalReason,
    DateTime CreatedAt,
    string BodyPreviewPlainText,
    string? MediaUrlPreview);

public sealed record ModerationMetricsWithAlerts(
    ContentModerationMetricsSnapshot Metrics,
    IReadOnlyList<ModerationAlertDto> Alerts);
