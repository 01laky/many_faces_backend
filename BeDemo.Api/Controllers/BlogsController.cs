using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Blogs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

/// <summary>CRUD and social features for blogs, including moderation-aware create/update paths.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BlogsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BlogsController> _logger;
    private readonly IRedisJobQueue _jobQueue;
    private readonly IFaceScopeContext _faceScope;
    private readonly IAccessEvaluator _access;
    /// <summary>Queues in-app notifications when user content enters the moderation pipeline.</summary>
    private readonly IContentModerationNotifier _moderationNotifier;
    private readonly IOperatorAiSystemSettingsProvider _systemSettings;

    public BlogsController(
        ApplicationDbContext context,
        ILogger<BlogsController> logger,
        IRedisJobQueue jobQueue,
        IFaceScopeContext faceScope,
        IAccessEvaluator access,
        IContentModerationNotifier moderationNotifier,
        IOperatorAiSystemSettingsProvider systemSettings)
    {
        _context = context;
        _logger = logger;
        _jobQueue = jobQueue;
        _faceScope = faceScope;
        _access = access;
        _moderationNotifier = moderationNotifier;
        _systemSettings = systemSettings;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

    /// <summary>GET /api/blogs?faceId= - Paginated blogs for scoped face.</summary>
    [HttpGet]
    public async Task<IActionResult> GetBlogs([FromQuery] BlogListQuery listQuery)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var operatorInventory = CanManageAllFaces();
        var page = listQuery.Page;
        var pageSize = listQuery.PageSize;

        IQueryable<Blog> query = _context.Blogs.AsNoTracking();
        query = OperatorContentListFilters.ApplyBlogPortalVisibility(query, operatorInventory);

        if (!string.IsNullOrWhiteSpace(listQuery.CreatorId))
        {
            var creatorId = listQuery.CreatorId.Trim();
            query = query.Where(b => b.CreatorId == creatorId);
            if (listQuery.FaceId is > 0)
            {
                var scopedFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
                query = query.Where(b => b.FaceId == scopedFaceId);
            }
        }
        else
        {
            var effectiveFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
            query = query.Where(b => b.FaceId == effectiveFaceId);
        }

        if (!string.IsNullOrWhiteSpace(listQuery.Search))
        {
            var pattern = $"%{listQuery.Search.Trim()}%";
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, pattern) ||
                EF.Functions.ILike(b.Content, pattern));
        }

        if (!string.IsNullOrWhiteSpace(listQuery.ApprovalStatus) &&
            Enum.TryParse<ContentApprovalStatus>(listQuery.ApprovalStatus, true, out var approvalFilter))
        {
            query = query.Where(b => b.ApprovalStatus == approvalFilter);
        }

        var totalCount = await query.CountAsync();
        var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
        page = clampedPage;

        var blogs = await ListSortApplicators
            .ApplyBlogsSort(query, listQuery.SortBy, listQuery.SortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.Title,
                b.Content,
                b.FaceId,
                faceTitle = b.Face.Title,
                creatorId = b.CreatorId,
                creatorName = (b.Creator.FirstName ?? "") + " " + (b.Creator.LastName ?? ""),
                images = b.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.ImageUrl, i.SortOrder }),
                imageCount = b.Images.Count,
                likesCount = b.Likes.Count,
                commentsCount = b.Comments.Count,
                approvalStatus = b.ApprovalStatus.ToString(),
                aiReviewStatus = b.AiReviewStatus.ToString(),
                creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(b.ApprovalStatus, b.AiReviewStatus),
                b.CreatedAt,
                b.UpdatedAt,
            })
            .ToListAsync();

        return Ok(ListPaginationHelper.BuildEnvelope(blogs, page, pageSize, totalCount, totalPages));
    }

    /// <summary>GET /api/blogs/{id} - Get blog by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBlog(int id, [FromQuery] BlogDetailQuery detailQuery)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var blog = await _context.Blogs
            .Include(b => b.Creator)
            .Include(b => b.Face)
            .Include(b => b.Images)
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (blog == null)
            return NotFound(new { error = "Blog not found" });

        var operatorInventory = CanManageAllFaces();
        var isCreator = blog.CreatorId == UserId;
        if (!operatorInventory && !isCreator && blog.ApprovalStatus != ContentApprovalStatus.Approved)
            return NotFound(new { error = "Blog not found" });

        var effectiveFaceId = _faceScope.ResolveDataFaceId(detailQuery.FaceId);
        if (blog.FaceId != effectiveFaceId)
            return NotFound(new { error = "Blog not found" });

        var showModerationFields = operatorInventory || isCreator;
        var contentPlainText = ContentModerationPreviewText.ToPlainTextPreview(blog.Content);

        return Ok(new
        {
            blog.Id,
            blog.Title,
            blog.Content,
            contentPlainText,
            blog.FaceId,
            faceTitle = blog.Face.Title,
            creatorId = blog.CreatorId,
            creatorName = (blog.Creator.FirstName ?? "") + " " + (blog.Creator.LastName ?? ""),
            images = blog.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.ImageUrl, i.SortOrder }),
            imageCount = blog.Images.Count,
            likesCount = blog.Likes.Count,
            commentsCount = blog.Comments.Count,
            isLikedByMe = blog.Likes.Any(l => l.UserId == UserId),
            approvalStatus = blog.ApprovalStatus.ToString(),
            aiReviewStatus = blog.AiReviewStatus.ToString(),
            aiReviewUserMessage = showModerationFields ? blog.AiReviewUserMessage : null,
            humanDecisionReason = showModerationFields ? blog.HumanDecisionReason : null,
            submittedAtUtc = showModerationFields ? blog.SubmittedAtUtc : null,
            removedAtUtc = showModerationFields ? blog.RemovedAtUtc : null,
            removalReason = showModerationFields ? blog.RemovalReason : null,
            aiReviewDecision = showModerationFields ? blog.AiReviewDecision.ToString() : null,
            aiReviewRiskLevel = showModerationFields ? blog.AiReviewRiskLevel.ToString() : null,
            aiReviewFlagsJson = showModerationFields ? blog.AiReviewFlagsJson : null,
            aiReviewReason = showModerationFields ? blog.AiReviewReason : null,
            aiReviewModelVersion = showModerationFields ? blog.AiReviewModelVersion : null,
            aiReviewTraceId = showModerationFields ? blog.AiReviewTraceId : null,
            aiReviewConfidence = showModerationFields ? blog.AiReviewConfidence : null,
            creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(blog.ApprovalStatus, blog.AiReviewStatus),
            blog.CreatedAt,
            blog.UpdatedAt,
        });
    }

    /// <summary>POST /api/blogs - Create blog</summary>
    [HttpPost]
    public async Task<IActionResult> CreateBlog([FromBody] CreateBlogDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var faceExists = await _context.Faces.AnyAsync(f => f.Id == dto.FaceId);
        if (!faceExists)
            return BadRequest(new { error = "Face not found" });

        var aiEnabled = await _systemSettings.IsAiEnabledAsync();
        var initialAiStatus = aiEnabled ? AiReviewStatus.Queued : AiReviewStatus.NeedsHumanReview;

        var blog = new Blog
        {
            CreatorId = UserId,
            FaceId = dto.FaceId,
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            ApprovalStatus = ContentApprovalStatus.PendingApproval,
            AiReviewStatus = initialAiStatus,
            SubmittedAtUtc = DateTime.UtcNow,
        };

        _context.Blogs.Add(blog);
        await _context.SaveChangesAsync();

        // Add images (max 3)
        if (dto.ImageUrls != null)
        {
            var urls = dto.ImageUrls.Take(3).ToList();
            for (int i = 0; i < urls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(urls[i]))
                {
                    _context.BlogImages.Add(new BlogImage
                    {
                        BlogId = blog.Id,
                        ImageUrl = urls[i].Trim(),
                        SortOrder = i,
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        _context.AiReviewJobs.Add(new AiReviewJob
        {
            ContentType = ModeratedContentType.Blog,
            ContentId = blog.Id,
            FaceId = blog.FaceId,
            CreatedByUserId = UserId,
            Status = aiEnabled ? AiReviewJobStatus.Queued : AiReviewJobStatus.NeedsHumanReview,
            ModerationVersion = blog.ModerationVersion,
            MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = aiEnabled ? null : DateTime.UtcNow,
            LastError = aiEnabled ? null : "AI support disabled.",
        });
        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            ModeratedContentType.Blog,
            blog.Id,
            blog.FaceId,
            null,
            blog.ApprovalStatus,
            AiReviewStatus.NotQueued,
            blog.AiReviewStatus,
            ModerationActorType.User,
            UserId,
            "Content submitted for approval.",
            "Your content was created and is waiting for review."));
        // Creator + super-admin notifications: safe copy only; detailed AI diagnostics stay server-side until admin review.
        _moderationNotifier.NotifyCreator(
            UserId,
            "Submitted for approval",
            "Your blog was submitted and is waiting for review.",
            "content_moderation");
        await _moderationNotifier.NotifySuperAdminsAsync(
            "New pending submission",
            $"Blog #{blog.Id} is pending moderation.",
            "moderation_ops",
            CancellationToken.None);
        await _context.SaveChangesAsync();
        if (aiEnabled)
            await EnqueueAiReviewAsync(ModeratedContentType.Blog, blog.Id, blog.ModerationVersion);

        _logger.LogInformation("User {UserId} created blog {BlogId}", UserId, blog.Id);

        return CreatedAtAction(nameof(GetBlog), new { id = blog.Id }, new
        {
            blog.Id,
            blog.Title,
            blog.Content,
            blog.FaceId,
            blog.CreatorId,
            approvalStatus = blog.ApprovalStatus.ToString(),
            aiReviewStatus = blog.AiReviewStatus.ToString(),
            creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(blog.ApprovalStatus, blog.AiReviewStatus),
            blog.CreatedAt,
        });
    }

    /// <summary>PUT /api/blogs/{id} - Update blog (creator only)</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBlog(int id, [FromBody] UpdateBlogDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var blog = await _context.Blogs
            .Include(b => b.Images)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (blog == null)
            return NotFound(new { error = "Blog not found" });

        if (blog.CreatorId != UserId)
            return Forbid();

        if (!ContentModerationHelpers.IsCreatorEditable(blog.ApprovalStatus))
            return Conflict(new { error = "Only pending or rejected blogs can be edited by the creator" });

        if (dto.Title != null)
            blog.Title = dto.Title.Trim();
        if (dto.Content != null)
            blog.Content = dto.Content.Trim();
        if (dto.FaceId.HasValue)
        {
            var faceExists = await _context.Faces.AnyAsync(f => f.Id == dto.FaceId.Value);
            if (!faceExists)
                return BadRequest(new { error = "Face not found" });
            blog.FaceId = dto.FaceId.Value;
        }

        // Update images if provided
        if (dto.ImageUrls != null)
        {
            _context.BlogImages.RemoveRange(blog.Images);
            var urls = dto.ImageUrls.Take(3).ToList();
            for (int i = 0; i < urls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(urls[i]))
                {
                    _context.BlogImages.Add(new BlogImage
                    {
                        BlogId = blog.Id,
                        ImageUrl = urls[i].Trim(),
                        SortOrder = i,
                    });
                }
            }
        }

        if (blog.ApprovalStatus == ContentApprovalStatus.Rejected)
        {
            var aiEnabled = await _systemSettings.IsAiEnabledAsync();
            blog.ApprovalStatus = ContentApprovalStatus.PendingApproval;
            blog.AiReviewStatus = aiEnabled ? AiReviewStatus.Queued : AiReviewStatus.NeedsHumanReview;
            blog.SubmittedAtUtc = DateTime.UtcNow;
            blog.HumanReviewedAtUtc = null;
            blog.HumanReviewedByUserId = null;
            blog.HumanDecisionReason = null;
            blog.ModerationVersion++;
        }

        blog.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        if (blog.AiReviewStatus == AiReviewStatus.Queued && await _systemSettings.IsAiEnabledAsync())
            await EnqueueAiReviewAsync(ModeratedContentType.Blog, blog.Id, blog.ModerationVersion);

        _logger.LogInformation("User {UserId} updated blog {BlogId}", UserId, blog.Id);
        return Ok(new
        {
            blog.Id,
            blog.Title,
            blog.Content,
            blog.FaceId,
            blog.CreatorId,
            approvalStatus = blog.ApprovalStatus.ToString(),
            aiReviewStatus = blog.AiReviewStatus.ToString(),
            creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(blog.ApprovalStatus, blog.AiReviewStatus),
            blog.CreatedAt,
            blog.UpdatedAt,
        });
    }

    /// <summary>DELETE /api/blogs/{id} - Delete blog (creator only)</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBlog(int id)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var blog = await _context.Blogs.FindAsync(id);

        if (blog == null)
            return NotFound(new { error = "Blog not found" });

        if (blog.CreatorId != UserId)
            return Forbid();

        if (!ContentModerationHelpers.IsCreatorDeletable(blog.ApprovalStatus))
            return Conflict(new { error = "Only pending or rejected blogs can be deleted by the creator" });

        _context.Blogs.Remove(blog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted blog {BlogId}", UserId, blog.Id);
        return NoContent();
    }

    private async Task EnqueueAiReviewAsync(
        ModeratedContentType contentType,
        int contentId,
        int moderationVersion)
    {
        if (!await _systemSettings.IsAiEnabledAsync())
            return;

        try
        {
            await _jobQueue.EnqueueAsync(
                ContentModerationHelpers.AiReviewJobType,
                ContentModerationHelpers.BuildAiReviewPayload(contentType, contentId, moderationVersion),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue AI review for {ContentType} {ContentId}", contentType, contentId);
        }
    }
}
