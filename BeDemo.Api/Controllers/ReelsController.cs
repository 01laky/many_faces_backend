using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
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
public class ReelsController : ControllerBase
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

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

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
	public async Task<IActionResult> GetReel(int id, [FromQuery] ReelDetailQuery detailQuery)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = detailQuery.FaceId;
		var reel = await _context.Reels
			.Include(r => r.Creator)
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Include(r => r.Likes)
			.Include(r => r.Comments)
			.FirstOrDefaultAsync(r => r.Id == id);

		if (reel == null)
			return NotFound(new { error = "Reel not found" });

		var operatorInventory = CanManageAllFaces();
		var isCreator = reel.CreatorId == UserId;
		if (!operatorInventory && !isCreator && reel.ApprovalStatus != ContentApprovalStatus.Approved)
			return NotFound(new { error = "Reel not found" });

		var effectiveFaceId = _faceScope.ResolveDataFaceId(faceId);
		if (!ReelVisibility.IsVisibleForFace(reel, effectiveFaceId))
			return NotFound(new { error = "Reel not found" });

		var showModerationFields = operatorInventory || isCreator;

		return Ok(new
		{
			reel.Id,
			reel.Title,
			reel.Description,
			videoUrl = reel.VideoUrl,
			creatorId = reel.CreatorId,
			creatorName = (reel.Creator.FirstName ?? "") + " " + (reel.Creator.LastName ?? ""),
			faces = reel.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
			likesCount = reel.Likes.Count,
			commentsCount = reel.Comments.Count,
			isLikedByMe = reel.Likes.Any(l => l.UserId == UserId),
			approvalStatus = reel.ApprovalStatus.ToString(),
			aiReviewStatus = reel.AiReviewStatus.ToString(),
			aiReviewUserMessage = showModerationFields ? reel.AiReviewUserMessage : null,
			humanDecisionReason = showModerationFields ? reel.HumanDecisionReason : null,
			submittedAtUtc = showModerationFields ? reel.SubmittedAtUtc : null,
			aiReviewDecision = showModerationFields ? reel.AiReviewDecision.ToString() : null,
			aiReviewRiskLevel = showModerationFields ? reel.AiReviewRiskLevel.ToString() : null,
			aiReviewFlagsJson = showModerationFields ? reel.AiReviewFlagsJson : null,
			aiReviewReason = showModerationFields ? reel.AiReviewReason : null,
			aiReviewModelVersion = showModerationFields ? reel.AiReviewModelVersion : null,
			aiReviewTraceId = showModerationFields ? reel.AiReviewTraceId : null,
			creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(reel.ApprovalStatus, reel.AiReviewStatus),
			reel.CreatedAt,
			reel.UpdatedAt,
		});
	}

	[HttpGet("user/{userId}")]
	public async Task<IActionResult> GetReelsByUser(string userId, [FromQuery] ReelByUserQuery byUserQuery)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = byUserQuery.FaceId;
		var query = _context.Reels.Where(r => r.CreatorId == userId);
		if (userId != UserId)
			query = query.Where(r => r.ApprovalStatus == ContentApprovalStatus.Approved);

		if (faceId.HasValue)
		{
			query = query.Where(r =>
				!r.ReelFaces.Any() ||
				r.ReelFaces.Any(rf => rf.FaceId == faceId.Value));
		}

		var reels = await query
			.Include(r => r.Creator)
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Include(r => r.Likes)
			.Include(r => r.Comments)
			.OrderByDescending(r => r.CreatedAt)
			.Select(r => new
			{
				r.Id,
				r.Title,
				r.Description,
				videoUrl = r.VideoUrl,
				creatorId = r.CreatorId,
				creatorName = (r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? ""),
				faces = r.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
				likesCount = r.Likes.Count,
				commentsCount = r.Comments.Count,
				approvalStatus = r.ApprovalStatus.ToString(),
				aiReviewStatus = r.AiReviewStatus.ToString(),
				creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(r.ApprovalStatus, r.AiReviewStatus),
				r.CreatedAt,
				r.UpdatedAt,
			})
			.ToListAsync();

		return Ok(reels);
	}

	[HttpPost]
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
	public async Task<IActionResult> UpdateReel(int id, [FromBody] UpdateReelDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == id);

		if (reel == null)
			return NotFound(new { error = "Reel not found" });

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
				return BadRequest(new { error = "VideoUrl must be an absolute http or https URL" });
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
	public async Task<IActionResult> DeleteReel(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels.FindAsync(id);
		if (reel == null)
			return NotFound(new { error = "Reel not found" });

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

	private async Task<object?> LoadReelDetailAsync(int reelId, string currentUserId)
	{
		var reel = await _context.Reels
			.Include(r => r.Creator)
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Include(r => r.Likes)
			.Include(r => r.Comments)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null)
			return null;

		return new
		{
			reel.Id,
			reel.Title,
			reel.Description,
			videoUrl = reel.VideoUrl,
			creatorId = reel.CreatorId,
			creatorName = (reel.Creator.FirstName ?? "") + " " + (reel.Creator.LastName ?? ""),
			faces = reel.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
			likesCount = reel.Likes.Count,
			commentsCount = reel.Comments.Count,
			isLikedByMe = reel.Likes.Any(l => l.UserId == currentUserId),
			approvalStatus = reel.ApprovalStatus.ToString(),
			aiReviewStatus = reel.AiReviewStatus.ToString(),
			aiReviewUserMessage = reel.CreatorId == currentUserId ? reel.AiReviewUserMessage : null,
			humanDecisionReason = reel.CreatorId == currentUserId ? reel.HumanDecisionReason : null,
			creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(reel.ApprovalStatus, reel.AiReviewStatus),
			reel.CreatedAt,
			reel.UpdatedAt,
		};
	}

}
