using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>Processes a single Redis-delivered AI review job for albums, blogs, or reels.</summary>
public interface IContentAiReviewService
{
    Task ProcessQueuedReviewAsync(string payloadJson, CancellationToken cancellationToken = default);
}

/// <summary>Aggregates moderation + AI job counters for dashboards and alerting.</summary>
public interface IContentModerationMetrics
{
    Task<ContentModerationMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>Point-in-time snapshot materialized for <c>GET /api/contentmoderation/metrics</c> and alert evaluation.</summary>
public sealed record ContentModerationMetricsSnapshot(
    int PendingSubmissions,
    int AiQueuedJobs,
    int AiProcessingJobs,
    int AiFailedJobs,
    DateTime? OldestPendingSubmissionUtc,
    double? OldestPendingAgeHours,
    double? AverageReviewLatencyHours,
    double? P95ReviewLatencyHours,
    int ApprovedCount,
    int RejectedCount,
    int RemovedCount,
    int RecommendedApproveCount,
    int RecommendedRejectCount,
    int NeedsHumanReviewCount,
    int AiJobsLikelyTimeoutCount,
    IReadOnlyList<FlagCountDto> TopModerationFlags,
    IReadOnlyList<FacePendingCountDto> PendingSubmissionsByFace);

public sealed record FlagCountDto(string Flag, int Count);

public sealed record FacePendingCountDto(int FaceId, string FaceTitle, int PendingCount);

/// <summary>
/// Worker invoked by the Redis job host. Responsible for:
/// <list type="number">
/// <item>Idempotency (stale moderation versions, duplicate completions).</item>
/// <item>Calling the AI gRPC service and validating its recommendation.</item>
/// <item>Retry scheduling vs terminal failure with creator + super-admin notifications.</item>
/// </list>
/// </summary>
public sealed class ContentAiReviewService : IContentAiReviewService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _context;
    private readonly IAiGrpcService _aiGrpcService;
    private readonly IRedisJobQueue _queue;
    private readonly ILogger<ContentAiReviewService> _logger;
    private readonly IContentModerationNotifier _moderationNotifier;
    private readonly IOptions<ContentModerationSecurityOptions> _securityOptions;

    public ContentAiReviewService(
        ApplicationDbContext context,
        IAiGrpcService aiGrpcService,
        IRedisJobQueue queue,
        ILogger<ContentAiReviewService> logger,
        IContentModerationNotifier moderationNotifier,
        IOptions<ContentModerationSecurityOptions> securityOptions)
    {
        _context = context;
        _aiGrpcService = aiGrpcService;
        _queue = queue;
        _logger = logger;
        _moderationNotifier = moderationNotifier;
        _securityOptions = securityOptions;
    }

    public async Task ProcessQueuedReviewAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        var payload = ParsePayload(payloadJson);
        if (payload == null)
        {
            _logger.LogWarning("Dropping invalid AI review payload: {Payload}", payloadJson);
            return;
        }

        var item = await LoadItemAsync(payload.ContentType, payload.ContentId, cancellationToken);
        if (item == null)
        {
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Failed, "Content no longer exists.", cancellationToken);
            return;
        }

        // Creator edited/resubmitted after this job was queued: never apply stale AI results.
        if (item.ModerationVersion != payload.ModerationVersion)
        {
            _logger.LogInformation(
                "Ignoring stale AI review job for {ContentType}:{ContentId}; payload v{PayloadVersion}, current v{CurrentVersion}",
                payload.ContentType,
                payload.ContentId,
                payload.ModerationVersion,
                item.ModerationVersion);
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Failed, "Stale moderation version.", cancellationToken);
            return;
        }

        // Human moderation may have raced ahead of the worker; treat as successful no-op for the job.
        if (item.ApprovalStatus != ContentApprovalStatus.PendingApproval)
        {
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Completed, "Content no longer pending.", cancellationToken);
            return;
        }

        var job = await GetOrCreateJobAsync(item, payload, cancellationToken);
        // Skip duplicate deliveries: job already finished or escalated to human review.
        if (job.Status is AiReviewJobStatus.Completed or AiReviewJobStatus.NeedsHumanReview)
            return;
        // Failed jobs may be retried until attempts reach MaxAttempts (checked after increment in the caller path).
        if (job.Status == AiReviewJobStatus.Failed && job.Attempts >= job.MaxAttempts)
            return;

        var oldAiStatus = item.AiReviewStatus;
        // Transition job + entity into an in-progress AI state visible to moderators.
        job.Status = AiReviewJobStatus.Processing;
        job.Attempts += 1;
        job.StartedAtUtc = DateTime.UtcNow;
        job.LastError = null;
        item.AiReviewStatus = AiReviewStatus.InProgress;
        AddEvent(item, oldAiStatus, AiReviewStatus.InProgress, ModerationActorType.System, "AI review started.");
        await _context.SaveChangesAsync(cancellationToken);

        // gRPC call into many_faces_ai; sanitize untrusted fields before they enter the AI process (defense in depth).
        var baseRequest = item.ToAiRequest();
        var (sanTitle, sanBody, sanMedia) = ContentModerationInputSanitizer.SanitizeForAiReview(
            baseRequest.Title,
            baseRequest.Body,
            baseRequest.MediaUrl);
        var aiRequest = baseRequest with
        {
            Title = sanTitle,
            Body = sanBody,
            MediaUrl = sanMedia,
        };
        var result = await _aiGrpcService.ReviewContentAsync(aiRequest, cancellationToken);
        if (result.Recommendation == null)
        {
            await HandleFailureAsync(item, job, result.Error ?? "AI review failed.", cancellationToken);
            return;
        }

        var merged = result.Recommendation;
        // Heuristic runs on stored entity fields (not sanitized wire copy) so smuggled bytes in DB still escalate.
        if (_securityOptions.Value.InstructionHeuristicEnabled &&
            ContentModerationPromptInjectionHeuristic.IsInstructionLike(item.Title, item.Body, item.MediaUrl))
        {
            var flags = merged.Flags.ToList();
            var needle = ContentModerationPromptInjectionHeuristic.InstructionLikeFlag;
            if (!flags.Exists(f => string.Equals(f, needle, StringComparison.OrdinalIgnoreCase)))
                flags.Add(needle);
            merged = merged with { Flags = flags };
        }

        merged = merged with { Flags = ContentModerationHelpers.NormalizeAiFlags(merged.Flags) };
        var validation = ContentModerationHelpers.ValidateRecommendation(merged);
        ApplyRecommendation(item, merged, validation);
        job.CompletedAtUtc = DateTime.UtcNow;
        job.LastError = validation.FallbackReason;
        // NeedsHumanReview is both an AI status and a terminal job disposition when validation fails.
        job.Status = item.AiReviewStatus == AiReviewStatus.NeedsHumanReview
            ? AiReviewJobStatus.NeedsHumanReview
            : AiReviewJobStatus.Completed;

        AddEvent(
            item,
            AiReviewStatus.InProgress,
            item.AiReviewStatus,
            ModerationActorType.AI,
            validation.FallbackReason ?? merged.Reason,
            merged.UserMessage);

        _logger.LogInformation(
            "AI review completed for {ContentType}:{ContentId} v{ModerationVersion}: {AiReviewStatus} confidence={Confidence} risk={Risk}",
            item.ContentType,
            item.ContentId,
            item.ModerationVersion,
            item.AiReviewStatus,
            merged.Confidence,
            merged.RiskLevel);

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Parses the Redis payload JSON; returns null if the envelope is malformed.</summary>
    private static AiReviewPayload? ParsePayload(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("contentType", out var contentTypeEl) ||
                !Enum.TryParse<ModeratedContentType>(contentTypeEl.GetString(), true, out var contentType) ||
                !root.TryGetProperty("contentId", out var contentIdEl) ||
                !contentIdEl.TryGetInt32(out var contentId) ||
                !root.TryGetProperty("moderationVersion", out var versionEl) ||
                !versionEl.TryGetInt32(out var moderationVersion))
            {
                return null;
            }

            return new AiReviewPayload(contentType, contentId, moderationVersion);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ModeratedContentSnapshot?> LoadItemAsync(
        ModeratedContentType contentType,
        int contentId,
        CancellationToken cancellationToken)
    {
        switch (contentType)
        {
            case ModeratedContentType.Album:
                var album = await _context.Albums
                    .Include(a => a.AlbumFaces)
                    .FirstOrDefaultAsync(a => a.Id == contentId, cancellationToken);
                return album == null ? null : ModeratedContentSnapshot.ForAlbum(album);
            case ModeratedContentType.Blog:
                var blog = await _context.Blogs
                    .Include(b => b.Images)
                    .FirstOrDefaultAsync(b => b.Id == contentId, cancellationToken);
                return blog == null ? null : ModeratedContentSnapshot.ForBlog(blog);
            case ModeratedContentType.Reel:
                var reel = await _context.Reels
                    .Include(r => r.ReelFaces)
                    .FirstOrDefaultAsync(r => r.Id == contentId, cancellationToken);
                return reel == null ? null : ModeratedContentSnapshot.ForReel(reel);
            default:
                return null;
        }
    }

    private async Task<AiReviewJob> GetOrCreateJobAsync(
        ModeratedContentSnapshot item,
        AiReviewPayload payload,
        CancellationToken cancellationToken)
    {
        var job = await _context.AiReviewJobs
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(
                j => j.ContentType == payload.ContentType &&
                    j.ContentId == payload.ContentId &&
                    j.ModerationVersion == payload.ModerationVersion,
                cancellationToken);
        if (job != null)
            return job;

        job = new AiReviewJob
        {
            ContentType = payload.ContentType,
            ContentId = payload.ContentId,
            FaceId = item.FaceId,
            CreatedByUserId = item.CreatorId,
            Status = AiReviewJobStatus.Queued,
            ModerationVersion = payload.ModerationVersion,
            MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _context.AiReviewJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    /// <summary>Maps transient AI/gRPC errors to either a delayed retry or terminal human review.</summary>
    private async Task HandleFailureAsync(
        ModeratedContentSnapshot item,
        AiReviewJob job,
        string error,
        CancellationToken cancellationToken)
    {
        var oldAiStatus = item.AiReviewStatus;
        job.LastError = ContentModerationHelpers.RedactForAudit(error);
        if (job.Attempts < job.MaxAttempts)
        {
            // Exponential-style backoff is centralized in RetryDelay; reschedule the same payload.
            job.Status = AiReviewJobStatus.RetryScheduled;
            job.NextAttemptAtUtc = DateTime.UtcNow.Add(RetryDelay);
            item.AiReviewStatus = AiReviewStatus.Queued;
            await _queue.ScheduleAsync(
                ContentModerationHelpers.AiReviewJobType,
                ContentModerationHelpers.BuildAiReviewPayload(item.ContentType, item.ContentId, item.ModerationVersion),
                job.NextAttemptAtUtc.Value,
                cancellationToken);
            AddEvent(item, oldAiStatus, AiReviewStatus.Queued, ModerationActorType.System, error);
        }
        else
        {
            // Out of retries: freeze item in NeedsHumanReview and page operators + creator via notifications.
            job.Status = AiReviewJobStatus.NeedsHumanReview;
            job.CompletedAtUtc = DateTime.UtcNow;
            item.AiReviewStatus = AiReviewStatus.NeedsHumanReview;
            item.AiReviewDecision = AiReviewDecision.NeedsHumanReview;
            item.AiReviewReason = "AI review failed after retries.";
            item.AiReviewUserMessage = "Your content needs manual review.";
            AddEvent(item, oldAiStatus, AiReviewStatus.NeedsHumanReview, ModerationActorType.System, error);
            _moderationNotifier.NotifyCreator(
                item.CreatorId,
                "Content needs manual review",
                "AI review could not finish automatically. A moderator will review your submission.",
                "content_moderation");
            await _moderationNotifier.NotifySuperAdminsAsync(
                "AI review exhausted retries",
                $"{item.ContentType} #{item.ContentId} needs human review.",
                "moderation_ops",
                cancellationToken);
        }

        _logger.LogWarning(
            "AI review failed for {ContentType}:{ContentId} attempt {Attempt}/{MaxAttempts}: {Error}",
            item.ContentType,
            item.ContentId,
            job.Attempts,
            job.MaxAttempts,
            error);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Copies validated AI fields onto the moderated entity snapshot (in-memory wrapper around EF entities).</summary>
    private void ApplyRecommendation(
        ModeratedContentSnapshot item,
        AiReviewRecommendation recommendation,
        AiRecommendationValidationResult validation)
    {
        item.AiReviewDecision = validation.IsValid ? recommendation.Decision : AiReviewDecision.NeedsHumanReview;
        item.AiReviewConfidence = recommendation.Confidence;
        item.AiReviewRiskLevel = recommendation.RiskLevel;
        item.AiReviewFlagsJson = JsonSerializer.Serialize(recommendation.Flags);
        item.AiReviewReason = validation.FallbackReason ?? recommendation.Reason;
        item.AiReviewUserMessage = recommendation.UserMessage;
        item.AiReviewModelVersion = recommendation.ModelVersion;
        item.AiReviewTraceId = recommendation.TraceId;
        item.AiReviewedAtUtc = DateTime.UtcNow;
        item.AiReviewStatus = validation.IsValid
            ? recommendation.Decision switch
            {
                AiReviewDecision.Approve => AiReviewStatus.RecommendedApprove,
                AiReviewDecision.Reject => AiReviewStatus.RecommendedReject,
                _ => AiReviewStatus.NeedsHumanReview,
            }
            : AiReviewStatus.NeedsHumanReview;
    }

    private async Task MarkJobTerminalAsync(
        AiReviewPayload payload,
        AiReviewJobStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        var job = await _context.AiReviewJobs
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(
                j => j.ContentType == payload.ContentType &&
                    j.ContentId == payload.ContentId &&
                    j.ModerationVersion == payload.ModerationVersion,
                cancellationToken);
        if (job == null)
            return;

        job.Status = status;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.LastError = ContentModerationHelpers.RedactForAudit(reason);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private void AddEvent(
        ModeratedContentSnapshot item,
        AiReviewStatus? oldAiReviewStatus,
        AiReviewStatus? newAiReviewStatus,
        ModerationActorType actorType,
        string? reason,
        string? userMessage = null)
    {
        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            item.ContentType,
            item.ContentId,
            item.FaceId,
            item.ApprovalStatus,
            item.ApprovalStatus,
            oldAiReviewStatus,
            newAiReviewStatus,
            actorType,
            null,
            reason,
            userMessage,
            item.AiReviewTraceId,
            item.AiReviewModelVersion));
    }

    private sealed record AiReviewPayload(
        ModeratedContentType ContentType,
        int ContentId,
        int ModerationVersion);

    private sealed class ModeratedContentSnapshot
    {
        private readonly Func<ContentApprovalStatus> _getApprovalStatus;
        private readonly Func<AiReviewStatus> _getAiReviewStatus;
        private readonly Action<AiReviewStatus> _setAiReviewStatus;
        private readonly Action<AiReviewDecision> _setAiReviewDecision;
        private readonly Action<double?> _setAiReviewConfidence;
        private readonly Action<AiReviewRiskLevel> _setAiReviewRiskLevel;
        private readonly Action<string?> _setAiReviewFlagsJson;
        private readonly Action<string?> _setAiReviewReason;
        private readonly Action<string?> _setAiReviewUserMessage;
        private readonly Action<string?> _setAiReviewModelVersion;
        private readonly Action<string?> _setAiReviewTraceId;
        private readonly Action<DateTime?> _setAiReviewedAtUtc;
        private string? _aiReviewModelVersion;
        private string? _aiReviewTraceId;

        private ModeratedContentSnapshot(
            ModeratedContentType contentType,
            int contentId,
            int faceId,
            string creatorId,
            string title,
            string body,
            string? mediaUrl,
            int moderationVersion,
            Func<ContentApprovalStatus> getApprovalStatus,
            Func<AiReviewStatus> getAiReviewStatus,
            Action<AiReviewStatus> setAiReviewStatus,
            Action<AiReviewDecision> setAiReviewDecision,
            Action<double?> setAiReviewConfidence,
            Action<AiReviewRiskLevel> setAiReviewRiskLevel,
            Action<string?> setAiReviewFlagsJson,
            Action<string?> setAiReviewReason,
            Action<string?> setAiReviewUserMessage,
            Action<string?> setAiReviewModelVersion,
            Action<string?> setAiReviewTraceId,
            Action<DateTime?> setAiReviewedAtUtc)
        {
            ContentType = contentType;
            ContentId = contentId;
            FaceId = faceId;
            CreatorId = creatorId;
            Title = title;
            Body = body;
            MediaUrl = mediaUrl;
            ModerationVersion = moderationVersion;
            _getApprovalStatus = getApprovalStatus;
            _getAiReviewStatus = getAiReviewStatus;
            _setAiReviewStatus = setAiReviewStatus;
            _setAiReviewDecision = setAiReviewDecision;
            _setAiReviewConfidence = setAiReviewConfidence;
            _setAiReviewRiskLevel = setAiReviewRiskLevel;
            _setAiReviewFlagsJson = setAiReviewFlagsJson;
            _setAiReviewReason = setAiReviewReason;
            _setAiReviewUserMessage = setAiReviewUserMessage;
            _setAiReviewModelVersion = setAiReviewModelVersion;
            _setAiReviewTraceId = setAiReviewTraceId;
            _setAiReviewedAtUtc = setAiReviewedAtUtc;
        }

        public static ModeratedContentSnapshot ForAlbum(Album album) => new(
            ModeratedContentType.Album,
            album.Id,
            album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
            album.CreatorId,
            album.Title,
            album.Description ?? string.Empty,
            null,
            album.ModerationVersion,
            () => album.ApprovalStatus,
            () => album.AiReviewStatus,
            value => album.AiReviewStatus = value,
            value => album.AiReviewDecision = value,
            value => album.AiReviewConfidence = value,
            value => album.AiReviewRiskLevel = value,
            value => album.AiReviewFlagsJson = value,
            value => album.AiReviewReason = value,
            value => album.AiReviewUserMessage = value,
            value => album.AiReviewModelVersion = value,
            value => album.AiReviewTraceId = value,
            value => album.AiReviewedAtUtc = value);

        public static ModeratedContentSnapshot ForBlog(Blog blog) => new(
            ModeratedContentType.Blog,
            blog.Id,
            blog.FaceId,
            blog.CreatorId,
            blog.Title,
            blog.Content,
            blog.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            blog.ModerationVersion,
            () => blog.ApprovalStatus,
            () => blog.AiReviewStatus,
            value => blog.AiReviewStatus = value,
            value => blog.AiReviewDecision = value,
            value => blog.AiReviewConfidence = value,
            value => blog.AiReviewRiskLevel = value,
            value => blog.AiReviewFlagsJson = value,
            value => blog.AiReviewReason = value,
            value => blog.AiReviewUserMessage = value,
            value => blog.AiReviewModelVersion = value,
            value => blog.AiReviewTraceId = value,
            value => blog.AiReviewedAtUtc = value);

        public static ModeratedContentSnapshot ForReel(Reel reel) => new(
            ModeratedContentType.Reel,
            reel.Id,
            reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
            reel.CreatorId,
            reel.Title,
            reel.Description ?? string.Empty,
            reel.VideoUrl,
            reel.ModerationVersion,
            () => reel.ApprovalStatus,
            () => reel.AiReviewStatus,
            value => reel.AiReviewStatus = value,
            value => reel.AiReviewDecision = value,
            value => reel.AiReviewConfidence = value,
            value => reel.AiReviewRiskLevel = value,
            value => reel.AiReviewFlagsJson = value,
            value => reel.AiReviewReason = value,
            value => reel.AiReviewUserMessage = value,
            value => reel.AiReviewModelVersion = value,
            value => reel.AiReviewTraceId = value,
            value => reel.AiReviewedAtUtc = value);

        public ModeratedContentType ContentType { get; }
        public int ContentId { get; }
        public int FaceId { get; }
        public string CreatorId { get; }
        public string Title { get; }
        public string Body { get; }
        public string? MediaUrl { get; }
        public int ModerationVersion { get; }

        public ContentApprovalStatus ApprovalStatus => _getApprovalStatus();

        public AiReviewStatus AiReviewStatus
        {
            get => _getAiReviewStatus();
            set => _setAiReviewStatus(value);
        }

        public AiReviewDecision AiReviewDecision
        {
            set => _setAiReviewDecision(value);
        }

        public double? AiReviewConfidence
        {
            set => _setAiReviewConfidence(value);
        }

        public AiReviewRiskLevel AiReviewRiskLevel
        {
            set => _setAiReviewRiskLevel(value);
        }

        public string? AiReviewFlagsJson
        {
            set => _setAiReviewFlagsJson(value);
        }

        public string? AiReviewReason
        {
            set => _setAiReviewReason(value);
        }

        public string? AiReviewUserMessage
        {
            set => _setAiReviewUserMessage(value);
        }

        public string? AiReviewModelVersion
        {
            get => _aiReviewModelVersion;
            set
            {
                _aiReviewModelVersion = value;
                _setAiReviewModelVersion(value);
            }
        }

        public string? AiReviewTraceId
        {
            get => _aiReviewTraceId;
            set
            {
                _aiReviewTraceId = value;
                _setAiReviewTraceId(value);
            }
        }

        public DateTime? AiReviewedAtUtc
        {
            set => _setAiReviewedAtUtc(value);
        }

        public AiContentReviewRequest ToAiRequest() => new(
            ContentType,
            ContentId,
            ModerationVersion,
            FaceId,
            Title,
            Body,
            MediaUrl,
            CreatorId);
    }
}

/// <summary>
/// EF-backed metrics provider used by <c>ContentModerationController</c>.
/// Computes queue depth, AI pipeline health, histogram-style latencies, and lightweight flag histograms over pending items.
/// </summary>
public sealed class ContentModerationMetrics : IContentModerationMetrics
{
    private readonly ApplicationDbContext _context;

    public ContentModerationMetrics(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContentModerationMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // Pending = still awaiting human decision (AI may already have recommended approve/reject).
        var pendingAlbums = _context.Albums.Where(a => a.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingBlogs = _context.Blogs.Where(b => b.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingReels = _context.Reels.Where(r => r.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingCount =
            await pendingAlbums.CountAsync(cancellationToken) +
            await pendingBlogs.CountAsync(cancellationToken) +
            await pendingReels.CountAsync(cancellationToken);
        // Oldest submission timestamp drives queue-age SLAs in the alert evaluator.
        var oldestDates = new[]
        {
            await pendingAlbums.MinAsync(a => (DateTime?)a.SubmittedAtUtc, cancellationToken),
            await pendingBlogs.MinAsync(b => (DateTime?)b.SubmittedAtUtc, cancellationToken),
            await pendingReels.MinAsync(r => (DateTime?)r.SubmittedAtUtc, cancellationToken),
        };

        var latencies = await CollectReviewLatenciesHoursAsync(cancellationToken);
        var p95 = Percentile95(latencies);
        var topFlags = await CollectTopFlagsAsync(cancellationToken);
        var pendingFaces = await CollectPendingByFaceAsync(cancellationToken);
        // Heuristic: failed jobs whose LastError mentions "timeout" (case-insensitive) for ops dashboards.
        var timeoutJobs = await _context.AiReviewJobs.CountAsync(
            j => j.Status == AiReviewJobStatus.Failed &&
                j.LastError != null &&
                j.LastError.ToLower().Contains("timeout"),
            cancellationToken);

        return new ContentModerationMetricsSnapshot(
            pendingCount,
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Queued || j.Status == AiReviewJobStatus.RetryScheduled, cancellationToken),
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Processing, cancellationToken),
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Failed, cancellationToken),
            oldestDates.Where(d => d.HasValue).DefaultIfEmpty().Min(),
            CalculateAgeHours(oldestDates.Where(d => d.HasValue).DefaultIfEmpty().Min(), DateTime.UtcNow),
            latencies.Count == 0 ? null : latencies.Average(),
            p95,
            await CountApprovalStatusAsync(ContentApprovalStatus.Approved, cancellationToken),
            await CountApprovalStatusAsync(ContentApprovalStatus.Rejected, cancellationToken),
            await CountApprovalStatusAsync(ContentApprovalStatus.Removed, cancellationToken),
            await CountAiStatusAsync(AiReviewStatus.RecommendedApprove, cancellationToken),
            await CountAiStatusAsync(AiReviewStatus.RecommendedReject, cancellationToken),
            await CountAiStatusAsync(AiReviewStatus.NeedsHumanReview, cancellationToken),
            timeoutJobs,
            topFlags,
            pendingFaces);
    }

    /// <summary>Counts entities in a terminal approval status across all moderated types.</summary>
    private async Task<int> CountApprovalStatusAsync(ContentApprovalStatus status, CancellationToken cancellationToken) =>
        await _context.Albums.CountAsync(a => a.ApprovalStatus == status, cancellationToken) +
        await _context.Blogs.CountAsync(b => b.ApprovalStatus == status, cancellationToken) +
        await _context.Reels.CountAsync(r => r.ApprovalStatus == status, cancellationToken);

    /// <summary>Counts rows whose AI pipeline is in a particular <see cref="AiReviewStatus"/>.</summary>
    private async Task<int> CountAiStatusAsync(AiReviewStatus status, CancellationToken cancellationToken) =>
        await _context.Albums.CountAsync(a => a.AiReviewStatus == status, cancellationToken) +
        await _context.Blogs.CountAsync(b => b.AiReviewStatus == status, cancellationToken) +
        await _context.Reels.CountAsync(r => r.AiReviewStatus == status, cancellationToken);

    /// <summary>Human review latency samples (hours) used for average and P95 dashboard cards.</summary>
    private async Task<List<double>> CollectReviewLatenciesHoursAsync(CancellationToken cancellationToken)
    {
        var latencies = new List<double>();
        latencies.AddRange(await _context.Albums
            .Where(a => a.SubmittedAtUtc.HasValue && a.HumanReviewedAtUtc.HasValue)
            .Select(a => (a.HumanReviewedAtUtc!.Value - a.SubmittedAtUtc!.Value).TotalHours)
            .ToListAsync(cancellationToken));
        latencies.AddRange(await _context.Blogs
            .Where(b => b.SubmittedAtUtc.HasValue && b.HumanReviewedAtUtc.HasValue)
            .Select(b => (b.HumanReviewedAtUtc!.Value - b.SubmittedAtUtc!.Value).TotalHours)
            .ToListAsync(cancellationToken));
        latencies.AddRange(await _context.Reels
            .Where(r => r.SubmittedAtUtc.HasValue && r.HumanReviewedAtUtc.HasValue)
            .Select(r => (r.HumanReviewedAtUtc!.Value - r.SubmittedAtUtc!.Value).TotalHours)
            .ToListAsync(cancellationToken));
        return latencies;
    }

    /// <summary>Nearest-rank P95 over latency samples; null when there is no completed human review data.</summary>
    private static double? Percentile95(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.OrderBy(v => v).ToList();
        var idx = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
        idx = Math.Clamp(idx, 0, sorted.Count - 1);
        return sorted[idx];
    }

    /// <summary>
    /// Builds a histogram of AI flag strings for pending items only (bounded sample per entity type for query cost).
    /// </summary>
    private async Task<IReadOnlyList<FlagCountDto>> CollectTopFlagsAsync(CancellationToken cancellationToken)
    {
        var jsonSamples = new List<string?>();
        jsonSamples.AddRange(await _context.Albums
            .Where(a => a.ApprovalStatus == ContentApprovalStatus.PendingApproval && a.AiReviewFlagsJson != null)
            .Select(a => a.AiReviewFlagsJson)
            .Take(2000)
            .ToListAsync(cancellationToken));
        jsonSamples.AddRange(await _context.Blogs
            .Where(b => b.ApprovalStatus == ContentApprovalStatus.PendingApproval && b.AiReviewFlagsJson != null)
            .Select(b => b.AiReviewFlagsJson)
            .Take(2000)
            .ToListAsync(cancellationToken));
        jsonSamples.AddRange(await _context.Reels
            .Where(r => r.ApprovalStatus == ContentApprovalStatus.PendingApproval && r.AiReviewFlagsJson != null)
            .Select(r => r.AiReviewFlagsJson)
            .Take(2000)
            .ToListAsync(cancellationToken));

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in jsonSamples)
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;
            try
            {
                var flags = JsonSerializer.Deserialize<List<string>>(json);
                if (flags == null)
                    continue;
                foreach (var f in flags.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    counts.TryGetValue(f, out var c);
                    counts[f] = c + 1;
                }
            }
            catch (JsonException)
            {
                // Malformed JSON should not break the entire metrics query.
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .Take(12)
            .Select(kv => new FlagCountDto(kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>Rolls up pending submissions per face across blogs, albums, and reels for hotspot detection.</summary>
    private async Task<IReadOnlyList<FacePendingCountDto>> CollectPendingByFaceAsync(CancellationToken cancellationToken)
    {
        var map = new Dictionary<int, int>();
        var blogGroups = await _context.Blogs
            .Where(b => b.ApprovalStatus == ContentApprovalStatus.PendingApproval)
            .GroupBy(b => b.FaceId)
            .Select(g => new { FaceId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var g in blogGroups)
            map[g.FaceId] = map.GetValueOrDefault(g.FaceId) + g.Count;

        var albumGroups = await _context.Albums
            .Where(a => a.ApprovalStatus == ContentApprovalStatus.PendingApproval)
            .SelectMany(a => a.AlbumFaces)
            .GroupBy(af => af.FaceId)
            .Select(g => new { FaceId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var g in albumGroups)
            map[g.FaceId] = map.GetValueOrDefault(g.FaceId) + g.Count;

        var reelGroups = await _context.Reels
            .Where(r => r.ApprovalStatus == ContentApprovalStatus.PendingApproval)
            .SelectMany(r => r.ReelFaces)
            .GroupBy(rf => rf.FaceId)
            .Select(g => new { FaceId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var g in reelGroups)
            map[g.FaceId] = map.GetValueOrDefault(g.FaceId) + g.Count;

        var faceIds = map.Keys.ToList();
        if (faceIds.Count == 0)
            return Array.Empty<FacePendingCountDto>();

        var titles = await _context.Faces
            .Where(f => faceIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Title })
            .ToListAsync(cancellationToken);
        var titleById = titles.ToDictionary(t => t.Id, t => t.Title);

        return map
            .OrderByDescending(kv => kv.Value)
            .Take(20)
            .Select(kv => new FacePendingCountDto(
                kv.Key,
                titleById.GetValueOrDefault(kv.Key, $"Face {kv.Key}"),
                kv.Value))
            .ToList();
    }

    private static double? CalculateAgeHours(DateTime? dateTimeUtc, DateTime nowUtc) =>
        dateTimeUtc.HasValue ? Math.Max(0, (nowUtc - dateTimeUtc.Value).TotalHours) : null;
}
