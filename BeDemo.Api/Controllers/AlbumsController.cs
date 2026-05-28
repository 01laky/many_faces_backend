using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Albums;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Grid;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

/// <summary>Album CRUD with face scoping and moderation-aware submission paths.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<AlbumsController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IRedisJobQueue _jobQueue;
	/// <summary>Queues in-app notifications when albums enter the moderation pipeline.</summary>
	private readonly IContentModerationNotifier _moderationNotifier;
	private readonly IOperatorAiSystemSettingsProvider _systemSettings;
	private readonly IAlbumGridListService _albumGridList;

	public AlbumsController(
		ApplicationDbContext context,
		ILogger<AlbumsController> logger,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IRedisJobQueue jobQueue,
		IContentModerationNotifier moderationNotifier,
		IOperatorAiSystemSettingsProvider systemSettings,
		IAlbumGridListService albumGridList)
	{
		_context = context;
		_logger = logger;
		_faceScope = faceScope;
		_access = access;
		_jobQueue = jobQueue;
		_moderationNotifier = moderationNotifier;
		_systemSettings = systemSettings;
		_albumGridList = albumGridList;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>GET /api/albums?faceId= - Paginated list; operator inventory skips portal visibility (§1.1).</summary>
	[HttpGet]
	public async Task<IActionResult> GetAlbums([FromQuery] AlbumListQuery listQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _albumGridList.GetAlbumsAsync(User, UserId, listQuery, cancellationToken);
		return Ok(result);
	}

	/// <summary>GET /api/albums/{id} - Get album by ID</summary>
	[HttpGet("{id}")]
	public async Task<IActionResult> GetAlbum(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums
			.Include(a => a.Creator)
			.Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
			.Include(a => a.MediaItems)
			.Include(a => a.Likes)
			.Include(a => a.Comments)
			.FirstOrDefaultAsync(a => a.Id == id);

		if (album == null)
			return NotFound(new { error = "Album not found" });

		var operatorInventory = CanManageAllFaces();
		var isCreator = album.CreatorId == UserId;

		if (!operatorInventory)
		{
			if (!isCreator && album.ApprovalStatus != ContentApprovalStatus.Approved)
				return NotFound(new { error = "Album not found" });
			if (album.AlbumType != AlbumTypeEnum.Public && !isCreator)
				return Forbid();
		}

		var effectiveFaceId = _faceScope.ResolveDataFaceId(
			Request.Query.TryGetValue("faceId", out var qf) && int.TryParse(qf.FirstOrDefault(), out var qid) ? qid : null);
		if (!album.AlbumFaces.Any(af => af.FaceId == effectiveFaceId))
			return NotFound(new { error = "Album not found" });

		var showModerationFields = operatorInventory || isCreator;

		return Ok(new
		{
			album.Id,
			album.Title,
			album.Description,
			albumType = (int)album.AlbumType,
			mediaType = (int)album.MediaType,
			creatorId = album.CreatorId,
			creatorName = (album.Creator.FirstName ?? "") + " " + (album.Creator.LastName ?? ""),
			faces = album.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
			likesCount = album.Likes.Count,
			commentsCount = album.Comments.Count,
			isLikedByMe = album.Likes.Any(l => l.UserId == UserId),
			approvalStatus = album.ApprovalStatus.ToString(),
			aiReviewStatus = album.AiReviewStatus.ToString(),
			aiReviewUserMessage = showModerationFields ? album.AiReviewUserMessage : null,
			humanDecisionReason = showModerationFields ? album.HumanDecisionReason : null,
			submittedAtUtc = showModerationFields ? album.SubmittedAtUtc : null,
			creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(album.ApprovalStatus, album.AiReviewStatus),
			mediaCount = album.MediaItems.Count,
			mediaItems = album.MediaItems
				.OrderBy(m => m.SortOrder)
				.Select(m => new
				{
					m.Id,
					mediaType = m.MediaType.ToString(),
					m.ImageUrl,
					m.VideoUrl,
					m.ThumbnailUrl,
					m.SortOrder,
					m.Title,
				}),
			album.CreatedAt,
			album.UpdatedAt,
		});
	}

	/// <summary>GET /api/albums/user/{userId} - Get albums by user</summary>
	[HttpGet("user/{userId}")]
	public async Task<IActionResult> GetAlbumsByUser(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var query = _context.Albums.Where(a => a.CreatorId == userId);

		// Other users can only see public albums
		if (userId != UserId)
			query = query.Where(a =>
				a.ApprovalStatus == ContentApprovalStatus.Approved &&
				a.AlbumType == AlbumTypeEnum.Public);

		var effectiveFaceId = _faceScope.ResolveDataFaceId(
			Request.Query.TryGetValue("faceId", out var qf) && int.TryParse(qf.FirstOrDefault(), out var qid) ? qid : null);
		query = query.Where(a => a.AlbumFaces.Any(af => af.FaceId == effectiveFaceId));

		var albums = await query
			.Include(a => a.Creator)
			.Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
			.Include(a => a.Likes)
			.Include(a => a.Comments)
			.OrderByDescending(a => a.CreatedAt)
			.Select(a => new
			{
				a.Id,
				a.Title,
				a.Description,
				albumType = (int)a.AlbumType,
				mediaType = (int)a.MediaType,
				creatorId = a.CreatorId,
				creatorName = (a.Creator.FirstName ?? "") + " " + (a.Creator.LastName ?? ""),
				faces = a.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
				likesCount = a.Likes.Count,
				commentsCount = a.Comments.Count,
				mediaCount = a.MediaItems.Count,
				approvalStatus = a.ApprovalStatus.ToString(),
				aiReviewStatus = a.AiReviewStatus.ToString(),
				creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(a.ApprovalStatus, a.AiReviewStatus),
				a.CreatedAt,
				a.UpdatedAt,
			})
			.ToListAsync();

		return Ok(albums);
	}

	/// <summary>POST /api/albums - Create album</summary>
	[HttpPost]
	public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var aiEnabled = await _systemSettings.IsAiEnabledAsync();
		var initialAiStatus = aiEnabled ? AiReviewStatus.Queued : AiReviewStatus.NeedsHumanReview;

		var album = new Album
		{
			CreatorId = UserId,
			Title = dto.Title.Trim(),
			Description = dto.Description?.Trim(),
			AlbumType = dto.AlbumType,
			MediaType = dto.MediaType,
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
			AiReviewStatus = initialAiStatus,
			SubmittedAtUtc = DateTime.UtcNow,
		};

		_context.Albums.Add(album);
		await _context.SaveChangesAsync();

		// Face links: tenants default to current scope when omitted; admins must supply FaceIds (or get only admin-face links).
		var targetFaceIds = dto.FaceIds;
		if (targetFaceIds == null || targetFaceIds.Count == 0)
		{
			if (!CanManageAllFaces())
				targetFaceIds = new List<int> { _faceScope.FaceId };
			else
				targetFaceIds = new List<int> { _faceScope.FaceId };
		}

		if (!CanManageAllFaces())
		{
			if (targetFaceIds.Any(fid => fid != _faceScope.FaceId))
				return BadRequest(new { error = "Albums may only be linked to the current face scope" });
		}

		var validFaceIds = await _context.Faces
			.Where(f => targetFaceIds.Contains(f.Id))
			.Select(f => f.Id)
			.ToListAsync();

		foreach (var fid in validFaceIds)
		{
			_context.AlbumFaces.Add(new AlbumFace { AlbumId = album.Id, FaceId = fid });
		}

		if (validFaceIds.Count > 0)
		{
			var firstFaceId = validFaceIds[0];
			_context.AiReviewJobs.Add(new AiReviewJob
			{
				ContentType = ModeratedContentType.Album,
				ContentId = album.Id,
				FaceId = firstFaceId,
				CreatedByUserId = UserId,
				Status = aiEnabled ? AiReviewJobStatus.Queued : AiReviewJobStatus.NeedsHumanReview,
				ModerationVersion = album.ModerationVersion,
				MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
				CreatedAtUtc = DateTime.UtcNow,
				CompletedAtUtc = aiEnabled ? null : DateTime.UtcNow,
				LastError = aiEnabled ? null : "AI support disabled.",
			});
			_context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
				ModeratedContentType.Album,
				album.Id,
				firstFaceId,
				null,
				album.ApprovalStatus,
				AiReviewStatus.NotQueued,
				album.AiReviewStatus,
				ModerationActorType.User,
				UserId,
				"Content submitted for approval.",
				"Your content was created and is waiting for review."));
			// Creator + super-admin notifications: safe copy only; detailed AI diagnostics stay server-side until admin review.
			_moderationNotifier.NotifyCreator(
				UserId,
				"Submitted for approval",
				"Your album was submitted and is waiting for review.",
				"content_moderation");
			await _moderationNotifier.NotifySuperAdminsAsync(
				"New pending submission",
				$"Album #{album.Id} is pending moderation.",
				"moderation_ops",
				CancellationToken.None);
			await _context.SaveChangesAsync();
			if (aiEnabled)
				await EnqueueAiReviewAsync(ModeratedContentType.Album, album.Id, album.ModerationVersion);
		}

		_logger.LogInformation("User {UserId} created album {AlbumId}", UserId, album.Id);

		return CreatedAtAction(nameof(GetAlbum), new { id = album.Id }, new
		{
			album.Id,
			album.Title,
			album.Description,
			albumType = (int)album.AlbumType,
			mediaType = (int)album.MediaType,
			album.CreatorId,
			approvalStatus = album.ApprovalStatus.ToString(),
			aiReviewStatus = album.AiReviewStatus.ToString(),
			creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(album.ApprovalStatus, album.AiReviewStatus),
			album.CreatedAt,
		});
	}

	/// <summary>PUT /api/albums/{id} - Update album (creator only)</summary>
	[HttpPut("{id}")]
	public async Task<IActionResult> UpdateAlbum(int id, [FromBody] UpdateAlbumDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums
			.Include(a => a.AlbumFaces)
			.FirstOrDefaultAsync(a => a.Id == id);

		if (album == null)
			return NotFound(new { error = "Album not found" });

		if (album.CreatorId != UserId)
			return Forbid();

		var editConflict = ContentCreatorMutationGuard.TryConflictIfNotEditable(
			album.ApprovalStatus,
			ContentCreatorMutationGuard.AlbumsContentKind);
		if (editConflict != null)
			return editConflict;

		var scopeFace = _faceScope.ResolveDataFaceId(null);
		if (!album.AlbumFaces.Any(af => af.FaceId == scopeFace))
			return NotFound(new { error = "Album not found" });

		if (dto.Title != null)
			album.Title = dto.Title.Trim();
		if (dto.Description != null)
			album.Description = dto.Description.Trim();
		if (dto.AlbumType.HasValue)
			album.AlbumType = dto.AlbumType.Value;
		if (dto.MediaType.HasValue)
			album.MediaType = dto.MediaType.Value;

		if (album.ApprovalStatus == ContentApprovalStatus.Rejected)
		{
			album.ApprovalStatus = ContentApprovalStatus.PendingApproval;
			album.AiReviewStatus = AiReviewStatus.Queued;
			album.SubmittedAtUtc = DateTime.UtcNow;
			album.HumanReviewedAtUtc = null;
			album.HumanReviewedByUserId = null;
			album.HumanDecisionReason = null;
			album.ModerationVersion++;
		}

		album.UpdatedAt = DateTime.UtcNow;

		// Update face associations if provided
		if (dto.FaceIds != null)
		{
			if (!CanManageAllFaces())
			{
				if (dto.FaceIds.Any(fid => fid != _faceScope.FaceId))
					return BadRequest(new { error = "Albums may only be linked to the current face scope" });
			}

			_context.AlbumFaces.RemoveRange(album.AlbumFaces);

			var validFaceIds = await _context.Faces
				.Where(f => dto.FaceIds.Contains(f.Id))
				.Select(f => f.Id)
				.ToListAsync();

			foreach (var faceId in validFaceIds)
			{
				_context.AlbumFaces.Add(new AlbumFace
				{
					AlbumId = album.Id,
					FaceId = faceId,
				});
			}
		}

		await _context.SaveChangesAsync();
		if (album.AiReviewStatus == AiReviewStatus.Queued)
			await EnqueueAiReviewAsync(ModeratedContentType.Album, album.Id, album.ModerationVersion);

		_logger.LogInformation("User {UserId} updated album {AlbumId}", UserId, album.Id);
		return Ok(new
		{
			album.Id,
			album.Title,
			album.Description,
			albumType = (int)album.AlbumType,
			mediaType = (int)album.MediaType,
			album.CreatorId,
			approvalStatus = album.ApprovalStatus.ToString(),
			aiReviewStatus = album.AiReviewStatus.ToString(),
			creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(album.ApprovalStatus, album.AiReviewStatus),
			album.CreatedAt,
			album.UpdatedAt,
		});
	}

	/// <summary>DELETE /api/albums/{id} - Delete album (creator only)</summary>
	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteAlbum(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums.Include(a => a.AlbumFaces).FirstOrDefaultAsync(a => a.Id == id);

		if (album == null)
			return NotFound(new { error = "Album not found" });

		if (album.CreatorId != UserId)
			return Forbid();

		var deleteConflict = ContentCreatorMutationGuard.TryConflictIfNotDeletable(
			album.ApprovalStatus,
			ContentCreatorMutationGuard.AlbumsContentKind);
		if (deleteConflict != null)
			return deleteConflict;

		var scopeFace = _faceScope.ResolveDataFaceId(null);
		if (!album.AlbumFaces.Any(af => af.FaceId == scopeFace))
			return NotFound(new { error = "Album not found" });

		_context.Albums.Remove(album);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} deleted album {AlbumId}", UserId, album.Id);
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
