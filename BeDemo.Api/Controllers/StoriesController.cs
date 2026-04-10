using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStoryLifecycleService _lifecycle;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StoriesController> _logger;
    private readonly IFaceScopeContext _faceScope;

    public StoriesController(
        ApplicationDbContext context,
        IStoryLifecycleService lifecycle,
        IWebHostEnvironment env,
        ILogger<StoriesController> logger,
        IFaceScopeContext faceScope)
    {
        _context = context;
        _lifecycle = lifecycle;
        _env = env;
        _logger = logger;
        _faceScope = faceScope;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private bool CanManageAllFaces() =>
        _faceScope.IsAdminFaceScope &&
        (User.IsInRole(UserRole.GlobalRoleNames.Admin) ||
         User.IsInRole(UserRole.GlobalRoleNames.SuperAdmin));

    /// <summary>
    /// Tenants may only mutate stories that are untargeted (draft) or targeted at their face.
    /// Global admins under /admin/ may touch any story.
    /// </summary>
    private IActionResult? GateStoryForScope(Story story)
    {
        if (CanManageAllFaces())
            return null;
        if (!story.StoryFaces.Any())
            return null;
        if (story.StoryFaces.All(sf => sf.FaceId != _faceScope.FaceId))
            return NotFound(new { error = "Story not found" });
        return null;
    }

    /// <summary>Published stories for the current face (non-host viewers only).</summary>
    [HttpGet]
    public async Task<IActionResult> ListForFace([FromQuery] int faceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var effectiveFaceId = _faceScope.ResolveDataFaceId(faceId);
        if (!await StoryViewerRules.ViewerHasFaceMembershipAsync(_context, UserId, effectiveFaceId, cancellationToken))
            return Ok(Array.Empty<object>());

        var now = DateTime.UtcNow;
        var stories = await _context.Stories
            .AsNoTracking()
            .Include(s => s.Creator)
            .Include(s => s.StoryFaces)
            .Include(s => s.Images)
            .Where(s =>
                s.State == StoryState.Published &&
                s.PublishedAt != null &&
                s.PublishedAt <= now &&
                s.ExpiresAt != null &&
                s.ExpiresAt > now)
            .OrderByDescending(s => s.PublishedAt)
            .ToListAsync(cancellationToken);

        var visible = stories
            .Where(s => StoryVisibility.IsTargetedForFace(s, effectiveFaceId))
            .ToList();

        var result = visible.Select(s => new
        {
            s.Id,
            s.Title,
            creatorId = s.CreatorId,
            creatorName = ((s.Creator.FirstName ?? "") + " " + (s.Creator.LastName ?? "")).Trim(),
            imageCount = s.Images.Count,
            coverUrl = s.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            s.PublishedAt,
            s.ExpiresAt,
        });

        return Ok(result);
    }

    /// <summary>Current user's stories (all states), optional face targeting filter.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> ListMine([FromQuery] int? faceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var query = _context.Stories
            .AsNoTracking()
            .Include(s => s.StoryFaces)
            .Include(s => s.Images)
            .Where(s => s.CreatorId == UserId);

        var filterFace = _faceScope.ResolveDataFaceId(faceId);
        if (faceId.HasValue || !_faceScope.IsAdminFaceScope)
        {
            query = query.Where(s =>
                !s.StoryFaces.Any() ||
                s.StoryFaces.Any(sf => sf.FaceId == filterFace));
        }

        var list = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.State,
                s.PublishedAt,
                s.ExpiresAt,
                s.ScheduledPublishAt,
                s.CreatedAt,
                imageCount = s.Images.Count,
                faceIds = s.StoryFaces.Select(sf => sf.FaceId).ToList(),
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, [FromQuery] int faceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var effectiveFaceId = _faceScope.ResolveDataFaceId(faceId);

        var story = await _context.Stories
            .Include(s => s.Creator)
            .Include(s => s.StoryFaces)
            .Include(s => s.Images)
            .Include(s => s.Likes)
            .Include(s => s.Comments)
            .Include(s => s.Views)
            .ThenInclude(v => v.Viewer)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story == null)
            return NotFound(new { error = "Story not found" });

        var isCreator = story.CreatorId == UserId;
        var now = DateTime.UtcNow;
        var isLive = story.State == StoryState.Published &&
                     story.PublishedAt.HasValue && story.PublishedAt <= now &&
                     story.ExpiresAt.HasValue && story.ExpiresAt > now;

        if (!isCreator)
        {
            if (!await StoryViewerRules.ViewerHasFaceMembershipAsync(_context, UserId, effectiveFaceId, cancellationToken))
                return NotFound(new { error = "Story not found" });

            if (!isLive || !StoryVisibility.IsTargetedForFace(story, effectiveFaceId))
                return NotFound(new { error = "Story not found" });
        }

        var images = story.Images.OrderBy(i => i.SortOrder).Select(i => new
        {
            i.Id,
            i.ImageUrl,
            i.Description,
            i.SortOrder,
        }).ToList();

        var creatorName = ((story.Creator.FirstName ?? "") + " " + (story.Creator.LastName ?? "")).Trim();

        if (isCreator)
        {
            var viewers = story.Views
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => new
                {
                    v.ViewerUserId,
                    viewerName = ((v.Viewer.FirstName ?? "") + " " + (v.Viewer.LastName ?? "")).Trim(),
                    v.ViewedAt,
                })
                .ToList();

            return Ok(new
            {
                story.Id,
                story.Title,
                story.State,
                story.CreatorId,
                creatorName,
                images,
                likesCount = story.Likes.Count,
                commentsCount = story.Comments.Count,
                isLikedByMe = story.Likes.Any(l => l.UserId == UserId),
                story.PublishedAt,
                story.ExpiresAt,
                story.ScheduledPublishAt,
                story.CreatedAt,
                viewCount = story.Views.Count,
                viewers,
            });
        }

        return Ok(new
        {
            story.Id,
            story.Title,
            story.State,
            story.CreatorId,
            creatorName,
            images,
            likesCount = story.Likes.Count,
            commentsCount = story.Comments.Count,
            isLikedByMe = story.Likes.Any(l => l.UserId == UserId),
            story.PublishedAt,
            story.ExpiresAt,
            story.ScheduledPublishAt,
            story.CreatedAt,
            viewCount = story.Views.Count,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStoryDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "Title is required" });

        var faceTargets = dto.FaceIds;
        if (faceTargets == null || faceTargets.Count == 0)
            faceTargets = new List<int> { _faceScope.FaceId };
        else if (!CanManageAllFaces())
        {
            if (faceTargets.Any(fid => fid != _faceScope.FaceId))
                return BadRequest(new { error = "Stories may only target the current face scope" });
        }

        await _lifecycle.EnsureRoomForNewStoryAsync(UserId, cancellationToken);

        var story = new Story
        {
            CreatorId = UserId,
            Title = dto.Title.Trim(),
            State = StoryState.Draft,
        };

        _context.Stories.Add(story);
        await _context.SaveChangesAsync(cancellationToken);

        var valid = await _context.Faces
            .Where(f => faceTargets.Contains(f.Id))
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        foreach (var fid in valid)
            _context.StoryFaces.Add(new StoryFace { StoryId = story.Id, FaceId = fid });
        if (valid.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new { story.Id });
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, [FromBody] PublishStoryDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var storyPre = await _context.Stories.Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (storyPre == null)
            return NotFound(new { error = "Story not found" });
        var pubGate = GateStoryForScope(storyPre);
        if (pubGate != null)
            return pubGate;

        var (ok, err) = await _lifecycle.TryPublishAsync(UserId, id, dto.ScheduledPublishAt, cancellationToken);
        if (!ok)
        {
            return err switch
            {
                "not_found" => NotFound(new { error = "Story not found" }),
                "forbidden" => Forbid(),
                "invalid_images" => BadRequest(new { error = "Story must have between 1 and 10 images before publish" }),
                "already_published" => BadRequest(new { error = "Story is still live" }),
                _ => BadRequest(new { error = err }),
            };
        }

        return Ok(new { published = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var story = await _context.Stories.Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (story == null)
            return NotFound(new { error = "Story not found" });
        if (story.CreatorId != UserId)
            return Forbid();
        var delGate = GateStoryForScope(story);
        if (delGate != null)
            return delGate;

        _context.Stories.Remove(story);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} deleted story {StoryId}", UserId, id);
        return NoContent();
    }

    /// <summary>Record a view (idempotent per viewer).</summary>
    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> RecordView(int id, [FromQuery] int faceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var effectiveFaceId = _faceScope.ResolveDataFaceId(faceId);

        if (!await StoryViewerRules.ViewerIsActiveNonHostInFaceAsync(_context, UserId, effectiveFaceId, cancellationToken))
            return Forbid();

        var story = await _context.Stories
            .Include(s => s.StoryFaces)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story == null)
            return NotFound(new { error = "Story not found" });

        var now = DateTime.UtcNow;
        if (story.State != StoryState.Published || story.PublishedAt > now || story.ExpiresAt <= now)
            return NotFound(new { error = "Story not found" });

        if (!StoryVisibility.IsTargetedForFace(story, effectiveFaceId))
            return NotFound(new { error = "Story not found" });

        var existing = await _context.StoryViews.FirstOrDefaultAsync(
            v => v.StoryId == id && v.ViewerUserId == UserId,
            cancellationToken);
        if (existing != null)
            return Ok(new { recorded = false });

        _context.StoryViews.Add(new StoryView
        {
            StoryId = id,
            ViewerUserId = UserId,
            ViewedAt = now,
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { recorded = true });
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadImage(
        int id,
        [FromForm] IFormFile file,
        [FromForm] string? description,
        [FromForm] int sortOrder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var story = await _context.Stories.Include(s => s.Images).Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (story == null)
            return NotFound(new { error = "Story not found" });
        if (story.CreatorId != UserId)
            return Forbid();

        var imgGate = GateStoryForScope(story);
        if (imgGate != null)
            return imgGate;

        if (story.State == StoryState.Published)
        {
            var now = DateTime.UtcNow;
            if (story.ExpiresAt > now)
                return BadRequest(new { error = "Cannot add images to a live story" });
        }

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required" });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only image uploads are allowed" });

        if (sortOrder < 0 || sortOrder > 9)
            return BadRequest(new { error = "sortOrder must be 0–9" });

        if (story.Images.Count >= 10)
            return BadRequest(new { error = "Maximum 10 images per story" });

        if (story.Images.Any(i => i.SortOrder == sortOrder))
            return BadRequest(new { error = "sortOrder already used" });

        var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;
        var dir = Path.Combine(webRoot, "uploads", "stories", id.ToString());
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || ext.Length > 10)
            ext = ".bin";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = $"/uploads/stories/{id}/{fileName}";
        var img = new StoryImage
        {
            StoryId = id,
            ImageUrl = url,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SortOrder = sortOrder,
        };
        _context.StoryImages.Add(img);
        story.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { img.Id, imageUrl = url, img.SortOrder });
    }
}

public class CreateStoryDto
{
    public string Title { get; set; } = string.Empty;
    public List<int>? FaceIds { get; set; }
}

public class PublishStoryDto
{
    /// <summary>When set and in the future, story stays <see cref="StoryState.Scheduled"/> until then.</summary>
    public DateTime? ScheduledPublishAt { get; set; }
}
