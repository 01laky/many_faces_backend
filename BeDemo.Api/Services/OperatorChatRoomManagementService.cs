using BeDemo.Api.Data;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Operator hard-delete for face chat rooms: lifecycle cleanup + optional creator platform DM.
/// Chat rooms are outside ModeratedContentType; reason is logged only.
/// </summary>
public sealed class OperatorChatRoomManagementService : IOperatorChatRoomManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IChatRoomLifecycleService _lifecycle;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly ILogger<OperatorChatRoomManagementService> _logger;

	public OperatorChatRoomManagementService(
		ApplicationDbContext context,
		IChatRoomLifecycleService lifecycle,
		IPlatformDirectMessageService platformDm,
		ILogger<OperatorChatRoomManagementService> logger)
	{
		_context = context;
		_lifecycle = lifecycle;
		_platformDm = platformDm;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> HardDeleteRoomAsync(
		string operatorUserId,
		int roomId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var room = await _context.FaceChatRooms
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);

		if (room == null)
			return true;

		var creatorId = room.CreatorUserId;
		var title = room.Title;

		_logger.LogInformation(
			"Operator {OperatorId} hard-deleting chat room {RoomId} on face {FaceId}: {Reason}",
			operatorUserId,
			roomId,
			faceId,
			reason.Trim());

		await _lifecycle.DeleteRoomCompletelyAsync(
			roomId,
			"operator_deleted",
			notifyCreatorIdleExpiry: false,
			cancellationToken);

		if (!string.IsNullOrEmpty(creatorId))
		{
			var body =
				$"Your chat room \"{title}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
			await TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
		}

		return true;
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
					"Platform DM after chat room delete failed: {ErrorCode} operator={OperatorId} creator={CreatorId}",
					errorCode,
					operatorUserId,
					creatorId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"Platform DM after chat room delete threw; operator={OperatorId} creator={CreatorId}",
				operatorUserId,
				creatorId);
		}
	}
}
