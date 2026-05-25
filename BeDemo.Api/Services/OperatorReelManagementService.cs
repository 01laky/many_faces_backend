using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Hard-deletes reels (operator Remove = Delete reel) and sends creator DMs without rolling back on messenger failure.
/// </summary>
public sealed class OperatorReelManagementService : IOperatorReelManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly ILogger<OperatorReelManagementService> _logger;

	public OperatorReelManagementService(
		ApplicationDbContext context,
		IPlatformDirectMessageService platformDm,
		ILogger<OperatorReelManagementService> logger)
	{
		_context = context;
		_platformDm = platformDm;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> HardDeleteReelAsync(
		string operatorUserId,
		int reelId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId, cancellationToken);

		// Idempotent: reel already gone or never on this face → treat as success (204).
		if (reel == null || !reel.ReelFaces.Any(rf => rf.FaceId == faceId))
			return true;

		var creatorId = reel.CreatorId;
		var title = reel.Title;
		var reelFaceId = reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault();

		// Audit while the row still exists (hard delete, not Removed status).
		_context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
			ModeratedContentType.Reel,
			reel.Id,
			reelFaceId,
			reel.ApprovalStatus,
			reel.ApprovalStatus,
			reel.AiReviewStatus,
			reel.AiReviewStatus,
			ModerationActorType.SuperAdmin,
			operatorUserId,
			reason.Trim(),
			userMessage.Trim(),
			reel.AiReviewTraceId,
			reel.AiReviewModelVersion));

		// Orphan pending jobs are acceptable; reel row delete cascades via FK where configured.
		_context.Reels.Remove(reel);
		await _context.SaveChangesAsync(cancellationToken);

		await SendReelRemovedDmBestEffortAsync(operatorUserId, creatorId, title, userMessage, cancellationToken);
		return true;
	}

	/// <inheritdoc />
	public Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string reelTitle,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var body =
			$"Your reel \"{reelTitle}\" was rejected by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		return TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private async Task SendReelRemovedDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string reelTitle,
		string userMessage,
		CancellationToken cancellationToken)
	{
		var body =
			$"Your reel \"{reelTitle}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private static string TruncateUserMessage(string userMessage)
	{
		var trimmed = userMessage.Trim();
		if (trimmed.Length <= PlatformDirectMessageRules.MaxContentLength)
			return trimmed;
		return trimmed[..(PlatformDirectMessageRules.MaxContentLength - 3)] + "...";
	}

	private async Task TrySendDmAsync(
		string operatorUserId,
		string creatorId,
		string content,
		CancellationToken cancellationToken)
	{
		try
		{
			var (errorCode, _) = await _platformDm.SendAsync(operatorUserId, creatorId, content, cancellationToken);
			if (errorCode != null)
			{
				_logger.LogWarning(
					"Platform DM after reel action failed: {ErrorCode} operator={OperatorId} creator={CreatorId}",
					errorCode,
					operatorUserId,
					creatorId);
			}
		}
		catch (Exception ex)
		{
			// Best-effort: delete already committed; do not propagate.
			_logger.LogWarning(
				ex,
				"Platform DM after reel action threw; operator={OperatorId} creator={CreatorId}",
				operatorUserId,
				creatorId);
		}
	}
}
