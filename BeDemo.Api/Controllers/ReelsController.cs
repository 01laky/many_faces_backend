using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.Reels;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Grid;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

/// <summary>Reel CRUD with multi-face visibility rules and moderation-aware submission paths.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReelsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IRedisJobQueue _jobQueue;
	private readonly ILogger<ReelsController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	/// <summary>Queues in-app notifications when reels enter the moderation pipeline.</summary>
	private readonly IContentModerationNotifier _moderationNotifier;
	private readonly IOperatorAiSystemSettingsProvider _systemSettings;
	private readonly IReelGridListService _reelGridList;

	public ReelsController(
		ApplicationDbContext context,
		IRedisJobQueue jobQueue,
		ILogger<ReelsController> logger,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IContentModerationNotifier moderationNotifier,
		IOperatorAiSystemSettingsProvider systemSettings,
		IReelGridListService reelGridList)
	{
		_context = context;
		_jobQueue = jobQueue;
		_logger = logger;
		_faceScope = faceScope;
		_access = access;
		_moderationNotifier = moderationNotifier;
		_systemSettings = systemSettings;
		_reelGridList = reelGridList;
	}

	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>GET /api/reels?faceId= - Paginated; operator sees all approval statuses for scoped face.</summary>
	[HttpGet]
	public async Task<IActionResult> GetReels([FromQuery] ReelListQuery listQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _reelGridList.GetReelsAsync(User, UserId, listQuery, cancellationToken);
		return Ok(result);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(ReelDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetReel(int id, [FromQuery] ReelDetailQuery detailQuery)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = detailQuery.FaceId;
		var reel = await _context.Reels
			.AsNoTracking()
			.Include(r => r.Creator)
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Include(r => r.Likes)
			.Include(r => r.Comments)
			.AsSplitQuery()
			.FirstOrDefaultAsync(r => r.Id == id);

		if (reel == null)
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var operatorInventory = CanManageAllFaces();
		var isCreator = reel.CreatorId == UserId;
		if (!operatorInventory && !isCreator && reel.ApprovalStatus != ContentApprovalStatus.Approved)
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var effectiveFaceId = _faceScope.ResolveDataFaceId(faceId);
		if (!ReelVisibility.IsVisibleForFace(reel, effectiveFaceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var showModerationFields = operatorInventory || isCreator;
		return Ok(ReelDetailDto.From(reel, UserId ?? string.Empty, showModerationFields));
	}

	[HttpGet("user/{userId}")]
	[ProducesResponseType(typeof(IEnumerable<ReelDetailDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetReelsByUser(string userId, [FromQuery] ReelByUserQuery byUserQuery)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = byUserQuery.FaceId;
		var query = _context.Reels.AsNoTracking().Where(r => r.CreatorId == userId);
		if (userId != UserId)
			query = query.Where(r => r.ApprovalStatus == ContentApprovalStatus.Approved);

		if (faceId.HasValue)
		{
			query = query.Where(r =>
				!r.ReelFaces.Any() ||
				r.ReelFaces.Any(rf => rf.FaceId == faceId.Value));
		}

		var reels = await query
			.OrderByDescending(r => r.CreatedAt)
			.Take(500)
			.Select(r => new ReelDetailDto
			{
				Id = r.Id,
				Title = r.Title,
				Description = r.Description,
				VideoUrl = r.VideoUrl,
				CreatorId = r.CreatorId,
				CreatorName = ((r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? "")).Trim(),
				Faces = r.ReelFaces.Select(rf => new ReelFaceDto { FaceId = rf.FaceId, Title = rf.Face.Title }),
				LikesCount = r.Likes.Count,
				CommentsCount = r.Comments.Count,
				ApprovalStatus = r.ApprovalStatus.ToString(),
				AiReviewStatus = r.AiReviewStatus.ToString(),
				CreatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(r.ApprovalStatus, r.AiReviewStatus),
				CreatedAt = r.CreatedAt,
				UpdatedAt = r.UpdatedAt,
			})
			.ToListAsync();

		return Ok(reels);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ReelDetailDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateReel([FromBody] CreateReelDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var aiEnabled = await _systemSettings.IsAiEnabledAsync();
		var initialAiStatus = aiEnabled ? AiReviewStatus.Queued : AiReviewStatus.NeedsHumanReview;

		var reel = new Reel
		{
			CreatorId = UserId,
			Title = dto.Title.Trim(),
			Description = dto.Description?.Trim(),
			VideoUrl = dto.VideoUrl.Trim(),
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
			AiReviewStatus = initialAiStatus,
			SubmittedAtUtc = DateTime.UtcNow,
		};

		_context.Reels.Add(reel);
		await _context.SaveChangesAsync();

		if (dto.FaceIds is { Count: > 0 })
		{
			var validFaceIds = await _context.Faces
				.Where(f => dto.FaceIds.Contains(f.Id))
				.Select(f => f.Id)
				.ToListAsync();

			foreach (var faceId in validFaceIds)
			{
				_context.ReelFaces.Add(new ReelFace { ReelId = reel.Id, FaceId = faceId });
			}

			await _context.SaveChangesAsync();
		}

		var firstFaceId = dto.FaceIds?.FirstOrDefault() ?? 0;
		if (firstFaceId > 0)
		{
			_context.AiReviewJobs.Add(new AiReviewJob
			{
				ContentType = ModeratedContentType.Reel,
				ContentId = reel.Id,
				FaceId = firstFaceId,
				CreatedByUserId = UserId,
				Status = aiEnabled ? AiReviewJobStatus.Queued : AiReviewJobStatus.NeedsHumanReview,
				ModerationVersion = reel.ModerationVersion,
				MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
				CreatedAtUtc = DateTime.UtcNow,
				CompletedAtUtc = aiEnabled ? null : DateTime.UtcNow,
				LastError = aiEnabled ? null : "AI support disabled.",
			});
			_context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
				ModeratedContentType.Reel,
				reel.Id,
				firstFaceId,
				null,
				reel.ApprovalStatus,
				AiReviewStatus.NotQueued,
				reel.AiReviewStatus,
				ModerationActorType.User,
				UserId,
				"Content submitted for approval.",
				"Your content was created and is waiting for review."));
			// Creator + super-admin notifications: safe copy only; detailed AI diagnostics stay server-side until admin review.
			_moderationNotifier.NotifyCreator(
				UserId,
				"Submitted for approval",
				"Your reel was submitted and is waiting for review.",
				"content_moderation");
			await _moderationNotifier.NotifySuperAdminsAsync(
				"New pending submission",
				$"Reel #{reel.Id} is pending moderation.",
				"moderation_ops",
				CancellationToken.None);
			await _context.SaveChangesAsync();
		}

		try
		{
			if (aiEnabled)
			{
				await _jobQueue.EnqueueAsync(
					ContentModerationHelpers.AiReviewJobType,
					ContentModerationHelpers.BuildAiReviewPayload(ModeratedContentType.Reel, reel.Id, reel.ModerationVersion),
					CancellationToken.None);
			}

			await _jobQueue.ScheduleAsync(
				"reel.postprocess",
				JsonSerializer.Serialize(new { reelId = reel.Id, phase = "delayed_check" }),
				DateTime.UtcNow.AddHours(24),
				CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to enqueue reel jobs for reel {ReelId}", reel.Id);
		}

		_logger.LogInformation("User {UserId} created reel {ReelId}", UserId, reel.Id);

		var created = await LoadReelDetailAsync(reel.Id, UserId);
		return CreatedAtAction(nameof(GetReel), new { id = reel.Id }, created);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(ReelDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateReel(int id, [FromBody] UpdateReelDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == id);

		if (reel == null)
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		if (reel.CreatorId != UserId)
			return Forbid();

		var editConflict = ContentCreatorMutationGuard.TryConflictIfNotEditable(
			reel.ApprovalStatus,
			ContentCreatorMutationGuard.ReelsContentKind);
		if (editConflict != null)
			return editConflict;

		if (dto.Title != null)
			reel.Title = dto.Title.Trim();
		if (dto.Description != null)
			reel.Description = dto.Description.Trim();
		if (dto.VideoUrl != null)
		{
			if (!ContentModerationHelpers.IsSafeHttpUrl(dto.VideoUrl))
				return BadRequest(new ErrorResponseDto { Error = "VideoUrl must be an absolute http or https URL" });
			reel.VideoUrl = dto.VideoUrl.Trim();
		}

		if (reel.ApprovalStatus == ContentApprovalStatus.Rejected)
		{
			reel.ApprovalStatus = ContentApprovalStatus.PendingApproval;
			reel.AiReviewStatus = AiReviewStatus.Queued;
			reel.SubmittedAtUtc = DateTime.UtcNow;
			reel.HumanReviewedAtUtc = null;
			reel.HumanReviewedByUserId = null;
			reel.HumanDecisionReason = null;
			reel.ModerationVersion++;
		}

		reel.UpdatedAt = DateTime.UtcNow;

		if (dto.FaceIds != null)
		{
			_context.ReelFaces.RemoveRange(reel.ReelFaces);
			var validFaceIds = await _context.Faces
				.Where(f => dto.FaceIds.Contains(f.Id))
				.Select(f => f.Id)
				.ToListAsync();

			foreach (var faceId in validFaceIds)
			{
				_context.ReelFaces.Add(new ReelFace { ReelId = reel.Id, FaceId = faceId });
			}
		}

		await _context.SaveChangesAsync();
		if (reel.AiReviewStatus == AiReviewStatus.Queued)
		{
			await _jobQueue.EnqueueAsync(
				ContentModerationHelpers.AiReviewJobType,
				ContentModerationHelpers.BuildAiReviewPayload(ModeratedContentType.Reel, reel.Id, reel.ModerationVersion),
				CancellationToken.None);
		}
		_logger.LogInformation("User {UserId} updated reel {ReelId}", UserId, reel.Id);

		var updated = await LoadReelDetailAsync(reel.Id, UserId);
		return Ok(updated);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteReel(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels.FindAsync(id);
		if (reel == null)
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		if (reel.CreatorId != UserId)
			return Forbid();

		var deleteConflict = ContentCreatorMutationGuard.TryConflictIfNotDeletable(
			reel.ApprovalStatus,
			ContentCreatorMutationGuard.ReelsContentKind);
		if (deleteConflict != null)
			return deleteConflict;

		_context.Reels.Remove(reel);
		await _context.SaveChangesAsync();
		_logger.LogInformation("User {UserId} deleted reel {ReelId}", UserId, id);
		return NoContent();
	}

	private async Task<ReelDetailDto?> LoadReelDetailAsync(int reelId, string currentUserId)
	{
		var reel = await _context.Reels
			.AsNoTracking()
			.Include(r => r.Creator)
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Include(r => r.Likes)
			.Include(r => r.Comments)
			.AsSplitQuery()
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null)
			return null;

		var showModerationFields = reel.CreatorId == currentUserId;
		return ReelDetailDto.From(reel, currentUserId, showModerationFields);
	}

}
