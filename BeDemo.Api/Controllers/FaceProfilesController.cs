using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Per-face user profiles: directory, detail, likes, comments, reviews.
/// </summary>
[ApiController]
[Route("api/faces/{faceId:int}/profiles")]
public class FaceProfilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FaceProfilesController> _logger;

    public FaceProfilesController(ApplicationDbContext context, ILogger<FaceProfilesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private IActionResult VisibilityDenied() =>
        string.IsNullOrEmpty(CurrentUserId) ? Unauthorized() : Forbid();

    private async Task<Face?> GetFaceAsync(int faceId, CancellationToken ct) =>
        await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId, ct);

    private async Task<UserFaceProfile?> ResolveTargetProfileAsync(int faceId, string targetUserId, CancellationToken ct)
    {
        var up = await _context.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == targetUserId, ct);
        if (up == null) return null;
        return await _context.UserFaceProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(ufp => ufp.FaceId == faceId && ufp.UserProfileId == up.Id, ct);
    }

    private async Task<UserFaceProfile?> ResolveTargetProfileTrackedAsync(int faceId, string targetUserId, CancellationToken ct)
    {
        var up = await _context.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == targetUserId, ct);
        if (up == null) return null;
        return await _context.UserFaceProfiles
            .FirstOrDefaultAsync(ufp => ufp.FaceId == faceId && ufp.UserProfileId == up.Id, ct);
    }

    /// <summary>
    /// GET — list users with a non-host role in this face (paginated).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ListProfiles(
        int faceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });

        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
            return VisibilityDenied();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var hostName = UserRole.FaceRoleNames.FaceHost;
        var eligibleUserIds = await (
            from ufr in _context.UserFaceRoles.AsNoTracking()
            join ur in _context.UserRoles.AsNoTracking() on ufr.UserRoleId equals ur.Id
            where ufr.FaceId == faceId && ur.Scope == RoleScope.Face && ur.Name != hostName
            select ufr.UserId
        ).Distinct().ToListAsync(ct);

        var total = eligibleUserIds.Count;
        var slice = eligibleUserIds
            .OrderBy(x => x)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = new List<object>();
        foreach (var uid in slice)
        {
            var profile = await _context.UserProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == uid, ct);
            if (profile == null) continue;
            var ufp = await _context.UserFaceProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.FaceId == faceId && x.UserProfileId == profile.Id, ct);
            var display = ufp?.DisplayName?.Trim();
            if (string.IsNullOrEmpty(display))
                display = profile.Nickname;
            var avatar = !string.IsNullOrWhiteSpace(ufp?.AvatarUrl) ? ufp!.AvatarUrl : profile.AvatarUrl;
            items.Add(new
            {
                userId = uid,
                displayName = display,
                avatarUrl = avatar,
            });
        }

        return Ok(new
        {
            items,
            totalCount = total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
        });
    }

    /// <summary>
    /// GET — profile card fields for a user in this face.
    /// </summary>
    [HttpGet("{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfile(int faceId, string userId, CancellationToken ct = default)
    {
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });

        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
            return VisibilityDenied();

        var up = await _context.UserProfiles.AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (up == null)
            return NotFound(new { error = "User not found" });

        var ufp = await _context.UserFaceProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FaceId == faceId && x.UserProfileId == up.Id, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var display = !string.IsNullOrWhiteSpace(ufp.DisplayName) ? ufp.DisplayName : up.Nickname;
        var avatar = !string.IsNullOrWhiteSpace(ufp.AvatarUrl) ? ufp.AvatarUrl : up.AvatarUrl;
        var viewerId = CurrentUserId;
        var liked = false;
        if (!string.IsNullOrEmpty(viewerId))
        {
            liked = await _context.UserFaceProfileLikes.AsNoTracking()
                .AnyAsync(l => l.UserFaceProfileId == ufp.Id && l.UserId == viewerId, ct);
        }

        return Ok(new
        {
            userId,
            displayName = display,
            nickname = up.Nickname,
            age = up.Age,
            rod = up.Rod,
            avatarUrl = avatar,
            createdAt = ufp.CreatedAt,
            faceAllowsRecensions = face.AllowRecensions,
            likedByMe = liked,
        });
    }

    /// <summary>
    /// POST — like a profile (idempotent).
    /// </summary>
    [HttpPost("{userId}/like")]
    [Authorize]
    public async Task<IActionResult> LikeProfile(int faceId, string userId, CancellationToken ct = default)
    {
        var viewerId = CurrentUserId!;
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, viewerId, ct))
            return VisibilityDenied();

        var ufp = await ResolveTargetProfileTrackedAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });
        var ownerUserId = await _context.UserProfiles.AsNoTracking()
            .Where(p => p.Id == ufp.UserProfileId)
            .Select(p => p.UserId)
            .FirstAsync(ct);
        if (ownerUserId == viewerId)
            return BadRequest(new { error = "Cannot like own profile" });

        var exists = await _context.UserFaceProfileLikes
            .AnyAsync(l => l.UserFaceProfileId == ufp.Id && l.UserId == viewerId, ct);
        if (!exists)
        {
            _context.UserFaceProfileLikes.Add(new UserFaceProfileLike
            {
                UserFaceProfileId = ufp.Id,
                UserId = viewerId,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { liked = true });
    }

    /// <summary>
    /// DELETE — remove like.
    /// </summary>
    [HttpDelete("{userId}/like")]
    [Authorize]
    public async Task<IActionResult> UnlikeProfile(int faceId, string userId, CancellationToken ct = default)
    {
        var viewerId = CurrentUserId!;
        var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var row = await _context.UserFaceProfileLikes
            .FirstOrDefaultAsync(l => l.UserFaceProfileId == ufp.Id && l.UserId == viewerId, ct);
        if (row != null)
        {
            _context.UserFaceProfileLikes.Remove(row);
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { liked = false });
    }

    /// <summary>
    /// GET — users who liked this profile.
    /// </summary>
    [HttpGet("{userId}/likes")]
    [AllowAnonymous]
    public async Task<IActionResult> ListLikers(int faceId, string userId, CancellationToken ct = default)
    {
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
            return VisibilityDenied();

        var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var likers = await _context.UserFaceProfileLikes.AsNoTracking()
            .Where(l => l.UserFaceProfileId == ufp.Id)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.UserId, l.CreatedAt })
            .ToListAsync(ct);

        return Ok(likers);
    }

    /// <summary>
    /// GET — comments on profile.
    /// </summary>
    [HttpGet("{userId}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> ListComments(int faceId, string userId, CancellationToken ct = default)
    {
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
            return VisibilityDenied();

        var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var list = await _context.UserFaceProfileComments.AsNoTracking()
            .Where(c => c.UserFaceProfileId == ufp.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.UserId, c.Body, c.CreatedAt })
            .ToListAsync(ct);

        return Ok(list);
    }

    /// <summary>
    /// POST — add comment.
    /// </summary>
    [HttpPost("{userId}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(int faceId, string userId, [FromBody] FaceProfileCommentDto dto, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Body))
            return BadRequest(new { error = "Body is required" });

        var viewerId = CurrentUserId!;
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, viewerId, ct))
            return VisibilityDenied();

        var ufp = await ResolveTargetProfileTrackedAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var c = new UserFaceProfileComment
        {
            UserFaceProfileId = ufp.Id,
            UserId = viewerId,
            Body = dto.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.UserFaceProfileComments.Add(c);
        await _context.SaveChangesAsync(ct);

        return Ok(new { c.Id, c.UserId, c.Body, c.CreatedAt });
    }

    /// <summary>
    /// DELETE — remove own comment.
    /// </summary>
    [HttpDelete("comments/{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int faceId, int commentId, CancellationToken ct = default)
    {
        var viewerId = CurrentUserId!;
        var c = await _context.UserFaceProfileComments
            .FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (c == null)
            return NotFound(new { error = "Comment not found" });

        var ufp = await _context.UserFaceProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == c.UserFaceProfileId, ct);
        if (ufp == null || ufp.FaceId != faceId)
            return NotFound(new { error = "Comment not found" });

        if (c.UserId != viewerId)
            return Forbid();

        _context.UserFaceProfileComments.Remove(c);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// GET — reviews (hidden when face disallows recensions).
    /// </summary>
    [HttpGet("{userId}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> ListReviews(int faceId, string userId, CancellationToken ct = default)
    {
        var face = await GetFaceAsync(faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
            return VisibilityDenied();
        if (!face.AllowRecensions)
            return Ok(Array.Empty<object>());

        var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var list = await _context.UserFaceProfileReviews.AsNoTracking()
            .Where(r => r.UserFaceProfileId == ufp.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.AuthorUserId, r.Title, r.Text, stars = r.Stars, r.CreatedAt })
            .ToListAsync(ct);

        return Ok(list);
    }

    /// <summary>
    /// POST — create/update review (one per author per profile). Stars 1–6.
    /// </summary>
    [HttpPost("{userId}/reviews")]
    [Authorize]
    public async Task<IActionResult> UpsertReview(int faceId, string userId, [FromBody] FaceProfileReviewDto dto, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Title and text are required" });
        if (dto.Stars is < 1 or > 6)
            return BadRequest(new { error = "Stars must be 1–6" });

        var authorId = CurrentUserId!;
        var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == faceId, ct);
        if (face == null)
            return NotFound(new { error = "Face not found" });
        if (!face.AllowRecensions)
            return BadRequest(new { error = "Reviews are disabled for this face" });
        if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, authorId, ct))
            return VisibilityDenied();

        var ufp = await ResolveTargetProfileTrackedAsync(faceId, userId, ct);
        if (ufp == null)
            return NotFound(new { error = "Face profile not found" });

        var up = await _context.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId, ct);
        if (up.UserId == authorId)
            return BadRequest(new { error = "Cannot review own profile" });

        var existing = await _context.UserFaceProfileReviews
            .FirstOrDefaultAsync(r => r.UserFaceProfileId == ufp.Id && r.AuthorUserId == authorId, ct);
        if (existing != null)
        {
            existing.Title = dto.Title.Trim();
            existing.Text = dto.Text.Trim();
            existing.Stars = (byte)dto.Stars!.Value;
            await _context.SaveChangesAsync(ct);
            return Ok(new { existing.Id, existing.AuthorUserId, existing.Title, existing.Text, stars = existing.Stars, existing.CreatedAt });
        }

        var r = new UserFaceProfileReview
        {
            UserFaceProfileId = ufp.Id,
            AuthorUserId = authorId,
            Title = dto.Title.Trim(),
            Text = dto.Text.Trim(),
            Stars = (byte)dto.Stars!.Value,
            CreatedAt = DateTime.UtcNow,
        };
        _context.UserFaceProfileReviews.Add(r);
        await _context.SaveChangesAsync(ct);
        return Ok(new { r.Id, r.AuthorUserId, r.Title, r.Text, stars = r.Stars, r.CreatedAt });
    }

    /// <summary>
    /// DELETE — author removes own review.
    /// </summary>
    [HttpDelete("reviews/{reviewId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(int faceId, int reviewId, CancellationToken ct = default)
    {
        var authorId = CurrentUserId!;
        var r = await _context.UserFaceProfileReviews.FirstOrDefaultAsync(x => x.Id == reviewId, ct);
        if (r == null)
            return NotFound(new { error = "Review not found" });

        var ufp = await _context.UserFaceProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == r.UserFaceProfileId, ct);
        if (ufp == null || ufp.FaceId != faceId)
            return NotFound(new { error = "Review not found" });

        if (r.AuthorUserId != authorId)
            return Forbid();

        _context.UserFaceProfileReviews.Remove(r);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class FaceProfileCommentDto
{
    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class FaceProfileReviewDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string Text { get; set; } = string.Empty;

    [Range(1, 6)]
    public int? Stars { get; set; }
}
