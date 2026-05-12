using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

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
    /// <summary>Queues in-app notifications when user content enters the moderation pipeline.</summary>
    private readonly IContentModerationNotifier _moderationNotifier;

    public BlogsController(
        ApplicationDbContext context,
        ILogger<BlogsController> logger,
        IRedisJobQueue jobQueue,
        IContentModerationNotifier moderationNotifier)
    {
        _context = context;
        _logger = logger;
        _jobQueue = jobQueue;
        _moderationNotifier = moderationNotifier;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>GET /api/blogs?faceId={faceId} - Get blogs for a face</summary>
    [HttpGet]
    public async Task<IActionResult> GetBlogs([FromQuery] int? faceId)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var query = _context.Blogs
            .Where(b => b.ApprovalStatus == ContentApprovalStatus.Approved)
            .AsQueryable();

        if (faceId.HasValue)
            query = query.Where(b => b.FaceId == faceId.Value);

        var blogs = await query
            .Include(b => b.Creator)
            .Include(b => b.Face)
            .Include(b => b.Images)
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .OrderByDescending(b => b.CreatedAt)
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
                likesCount = b.Likes.Count,
                commentsCount = b.Comments.Count,
                approvalStatus = b.ApprovalStatus.ToString(),
                aiReviewStatus = b.AiReviewStatus.ToString(),
                creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(b.ApprovalStatus, b.AiReviewStatus),
                b.CreatedAt,
                b.UpdatedAt,
            })
            .ToListAsync();

        return Ok(blogs);
    }

    /// <summary>GET /api/blogs/{id} - Get blog by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBlog(int id)
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

        var isCreator = blog.CreatorId == UserId;
        if (!isCreator && blog.ApprovalStatus != ContentApprovalStatus.Approved)
            return NotFound(new { error = "Blog not found" });

        return Ok(new
        {
            blog.Id,
            blog.Title,
            blog.Content,
            blog.FaceId,
            faceTitle = blog.Face.Title,
            creatorId = blog.CreatorId,
            creatorName = (blog.Creator.FirstName ?? "") + " " + (blog.Creator.LastName ?? ""),
            images = blog.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.ImageUrl, i.SortOrder }),
            likesCount = blog.Likes.Count,
            commentsCount = blog.Comments.Count,
            isLikedByMe = blog.Likes.Any(l => l.UserId == UserId),
            approvalStatus = blog.ApprovalStatus.ToString(),
            aiReviewStatus = blog.AiReviewStatus.ToString(),
            aiReviewUserMessage = isCreator ? blog.AiReviewUserMessage : null,
            humanDecisionReason = isCreator ? blog.HumanDecisionReason : null,
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

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "Title is required" });

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Content is required" });

        if (dto.FaceId <= 0)
            return BadRequest(new { error = "FaceId is required" });

        var faceExists = await _context.Faces.AnyAsync(f => f.Id == dto.FaceId);
        if (!faceExists)
            return BadRequest(new { error = "Face not found" });

        var blog = new Blog
        {
            CreatorId = UserId,
            FaceId = dto.FaceId,
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            ApprovalStatus = ContentApprovalStatus.PendingApproval,
            AiReviewStatus = AiReviewStatus.Queued,
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
            Status = AiReviewJobStatus.Queued,
            ModerationVersion = blog.ModerationVersion,
            MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
            CreatedAtUtc = DateTime.UtcNow,
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
            blog.ApprovalStatus = ContentApprovalStatus.PendingApproval;
            blog.AiReviewStatus = AiReviewStatus.Queued;
            blog.SubmittedAtUtc = DateTime.UtcNow;
            blog.HumanReviewedAtUtc = null;
            blog.HumanReviewedByUserId = null;
            blog.HumanDecisionReason = null;
            blog.ModerationVersion++;
        }

        blog.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        if (blog.AiReviewStatus == AiReviewStatus.Queued)
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

public class CreateBlogDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int FaceId { get; set; }
    public List<string>? ImageUrls { get; set; }
}

public class UpdateBlogDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int? FaceId { get; set; }
    public List<string>? ImageUrls { get; set; }
}
