using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Per-face user profiles: directory, detail, likes, comments, reviews.
/// </summary>
[ApiController]
[Route("api/faces/{faceId:int}/profiles")]
public partial class FaceProfilesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<FaceProfilesController> _logger;
	private readonly IUploadSignedUrlService _uploadUrls;
	private readonly IAccessEvaluator _access;

	public FaceProfilesController(
		ApplicationDbContext context,
		ILogger<FaceProfilesController> logger,
		IUploadSignedUrlService uploadUrls,
		IAccessEvaluator access)
	{
		_context = context;
		_logger = logger;
		_uploadUrls = uploadUrls;
		_access = access;
	}

	// Operator inventory reads (admin profile detail) use CanManageAllFaces, not ApprovalStatus.
	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	private IActionResult VisibilityDenied() =>
		string.IsNullOrEmpty(UserId) ? Unauthorized() : Forbid();

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

	/// <summary>GET — profile card fields for a user in this face (extended for operator inventory).</summary>
	[HttpGet("{userId}")]
	[AllowAnonymous]
	public async Task<IActionResult> GetProfile(int faceId, string userId, CancellationToken ct = default)
	{
		var face = await GetFaceAsync(faceId, ct);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var operatorInventory = CanManageAllFaces();
		if (!operatorInventory &&
			!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, UserId, ct))
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
		var viewerId = UserId;
		var liked = false;
		if (!string.IsNullOrEmpty(viewerId))
		{
			liked = await _context.UserFaceProfileLikes.AsNoTracking()
				.AnyAsync(l => l.UserFaceProfileId == ufp.Id && l.UserId == viewerId, ct);
		}

		var commentsCount = await _context.UserFaceProfileComments.AsNoTracking()
			.CountAsync(c => c.UserFaceProfileId == ufp.Id, ct);
		var likesCount = await _context.UserFaceProfileLikes.AsNoTracking()
			.CountAsync(l => l.UserFaceProfileId == ufp.Id, ct);
		var reviewsCount = face.AllowRecensions
			? await _context.UserFaceProfileReviews.AsNoTracking().CountAsync(r => r.UserFaceProfileId == ufp.Id, ct)
			: 0;

		var faceRoleName = await (
			from ufr in _context.UserFaceRoles.AsNoTracking()
			join ur in _context.UserRoles.AsNoTracking() on ufr.UserRoleId equals ur.Id
			where ufr.FaceId == faceId && ufr.UserId == userId
			select ur.Name
		).FirstOrDefaultAsync(ct) ?? "FACE_USER";

		var isFaceBanned = await _context.UserFaceModerations.AsNoTracking()
			.AnyAsync(m => m.UserId == userId && m.FaceId == faceId && m.LiftedAt == null, ct);

		return Ok(new
		{
			userId,
			userFaceProfileId = ufp.Id,
			displayName = display,
			nickname = up.Nickname,
			age = up.Age,
			rod = up.Rod,
			avatarUrl = _uploadUrls.ToAbsoluteSignedUrl(avatar, Request.Scheme, Request.Host.Value!),
			createdAt = ufp.CreatedAt,
			updatedAt = ufp.UpdatedAt ?? ufp.CreatedAt,
			faceAllowsRecensions = face.AllowRecensions,
			faceVisibility = face.Visibility.ToString(),
			faceRoleName,
			isActive = ufp.IsActive,
			visited = ufp.Visited,
			commentsCount,
			likesCount,
			reviewsCount,
			isFaceBanned,
			email = operatorInventory ? up.User?.Email : null,
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
		var viewerId = UserId!;
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
		var viewerId = UserId!;
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
		if (!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, UserId, ct))
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

	/// <summary>GET — comments on profile (portal array or operator paginated envelope when page &gt;= 1).</summary>
	[HttpGet("{userId}/comments")]
	[AllowAnonymous]
	public async Task<IActionResult> ListComments(
		int faceId,
		string userId,
		[FromQuery] FaceProfileCommentsListQuery listQuery,
		CancellationToken ct = default)
	{
		var face = await GetFaceAsync(faceId, ct);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var operatorInventory = CanManageAllFaces();
		if (!operatorInventory &&
			!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, UserId, ct))
			return VisibilityDenied();

		var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
		if (ufp == null)
			return NotFound(new { error = "Face profile not found" });

		if (listQuery.Page >= 1)
		{
			if (!operatorInventory)
				return Forbid();

			var page = listQuery.Page;
			var pageSize = listQuery.PageSize;
			var q = _context.UserFaceProfileComments.AsNoTracking()
				.Where(c => c.UserFaceProfileId == ufp.Id);

			if (!string.IsNullOrWhiteSpace(listQuery.Search))
			{
				var term = listQuery.Search.Trim();
				q = q.Where(c => c.Body.Contains(term) || c.UserId.Contains(term));
			}

			var totalCount = await q.CountAsync(ct);
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var desc = SortRules.IsDescending(listQuery.SortDir);
			var ordered = (listQuery.SortBy?.ToLowerInvariant()) switch
			{
				"createdat" => desc ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
				"userid" => desc ? q.OrderByDescending(c => c.UserId) : q.OrderBy(c => c.UserId),
				_ => q.OrderByDescending(c => c.CreatedAt),
			};

			var pageItems = await ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(c => new
				{
					c.Id,
					c.UserId,
					c.Body,
					c.CreatedAt,
					AuthorDisplayName = _context.UserProfiles.AsNoTracking()
						.Where(p => p.UserId == c.UserId)
						.Select(p => p.Nickname)
						.FirstOrDefault() ?? c.UserId,
				})
				.ToListAsync(ct);

			return Ok(ListPaginationHelper.BuildEnvelope(pageItems, page, pageSize, totalCount, totalPages));
		}

		// Portal: legacy full array when page query omitted.
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
		var viewerId = UserId!;
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
		var viewerId = UserId!;
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

	/// <summary>GET — reviews (portal array or operator envelope; empty when recensions off).</summary>
	[HttpGet("{userId}/reviews")]
	[AllowAnonymous]
	public async Task<IActionResult> ListReviews(
		int faceId,
		string userId,
		[FromQuery] FaceProfileReviewsListQuery listQuery,
		CancellationToken ct = default)
	{
		var face = await GetFaceAsync(faceId, ct);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var operatorInventory = CanManageAllFaces();
		if (!operatorInventory &&
			!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, UserId, ct))
			return VisibilityDenied();

		if (!face.AllowRecensions)
		{
			if (listQuery.Page >= 1 && operatorInventory)
				return Ok(ListPaginationHelper.BuildEnvelope(Array.Empty<object>(), 1, listQuery.PageSize, 0, 0));
			return Ok(Array.Empty<object>());
		}

		var ufp = await ResolveTargetProfileAsync(faceId, userId, ct);
		if (ufp == null)
			return NotFound(new { error = "Face profile not found" });

		if (listQuery.Page >= 1)
		{
			if (!operatorInventory)
				return Forbid();

			var page = listQuery.Page;
			var pageSize = listQuery.PageSize;
			var q = _context.UserFaceProfileReviews.AsNoTracking()
				.Where(r => r.UserFaceProfileId == ufp.Id);

			if (!string.IsNullOrWhiteSpace(listQuery.Search))
			{
				var term = listQuery.Search.Trim();
				q = q.Where(r =>
					r.Title.Contains(term) ||
					r.Text.Contains(term) ||
					r.AuthorUserId.Contains(term));
			}

			var totalCount = await q.CountAsync(ct);
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var desc = SortRules.IsDescending(listQuery.SortDir);
			var ordered = (listQuery.SortBy?.ToLowerInvariant()) switch
			{
				"createdat" => desc ? q.OrderByDescending(r => r.CreatedAt) : q.OrderBy(r => r.CreatedAt),
				"stars" => desc ? q.OrderByDescending(r => r.Stars) : q.OrderBy(r => r.Stars),
				"authoruserid" => desc ? q.OrderByDescending(r => r.AuthorUserId) : q.OrderBy(r => r.AuthorUserId),
				_ => q.OrderByDescending(r => r.CreatedAt),
			};

			var pageItems = await ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(r => new
				{
					r.Id,
					r.AuthorUserId,
					r.Title,
					r.Text,
					stars = r.Stars,
					r.CreatedAt,
					AuthorDisplayName = _context.UserProfiles.AsNoTracking()
						.Where(p => p.UserId == r.AuthorUserId)
						.Select(p => p.Nickname)
						.FirstOrDefault() ?? r.AuthorUserId,
				})
				.ToListAsync(ct);

			return Ok(ListPaginationHelper.BuildEnvelope(pageItems, page, pageSize, totalCount, totalPages));
		}

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
		if (dto.Stars is < 1 or > 6)
			return BadRequest(new { error = "Stars must be 1–6" });

		var authorId = UserId!;
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
		var authorId = UserId!;
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
