using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Hard-deletes albums (operator Remove = Delete album) and single media items;
/// writes audit events and sends creator DMs without rolling back on messenger failure.
/// </summary>
public sealed class OperatorAlbumManagementService : IOperatorAlbumManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly ILogger<OperatorAlbumManagementService> _logger;

	public OperatorAlbumManagementService(
		ApplicationDbContext context,
		IPlatformDirectMessageService platformDm,
		ILogger<OperatorAlbumManagementService> logger)
	{
		_context = context;
		_platformDm = platformDm;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> HardDeleteAlbumAsync(
		string operatorUserId,
		int albumId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var album = await _context.Albums
			.Include(a => a.AlbumFaces)
			.FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

		// Idempotent: album already gone or never on this face → treat as success (204).
		if (album == null || !album.AlbumFaces.Any(af => af.FaceId == faceId))
			return true;

		var creatorId = album.CreatorId;
		var title = album.Title;
		var albumFaceId = album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault();

		// Audit while the row still exists (hard delete, not Removed status).
		_context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
			ModeratedContentType.Album,
			album.Id,
			albumFaceId,
			album.ApprovalStatus,
			album.ApprovalStatus,
			album.AiReviewStatus,
			album.AiReviewStatus,
			ModerationActorType.SuperAdmin,
			operatorUserId,
			reason.Trim(),
			userMessage.Trim(),
			album.AiReviewTraceId,
			album.AiReviewModelVersion));

		_context.Albums.Remove(album);
		await _context.SaveChangesAsync(cancellationToken);

		await SendAlbumRemovedDmBestEffortAsync(operatorUserId, creatorId, title, userMessage, cancellationToken);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAlbumMediaAsync(
		string operatorUserId,
		int albumId,
		int mediaId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var album = await _context.Albums
			.Include(a => a.AlbumFaces)
			.Include(a => a.MediaItems)
			.FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

		if (album == null || !album.AlbumFaces.Any(af => af.FaceId == faceId))
			return false;

		var media = album.MediaItems.FirstOrDefault(m => m.Id == mediaId);
		if (media == null)
			return false;

		var itemTitle = ResolveMediaTitle(media);
		var albumTitle = album.Title;

		_context.AlbumMedia.Remove(media);
		await _context.SaveChangesAsync(cancellationToken);

		var body = BuildMediaRemovedMessage(itemTitle, albumTitle, userMessage);
		await TrySendDmAsync(operatorUserId, album.CreatorId, body, cancellationToken);
		return true;
	}

	/// <inheritdoc />
	public Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string albumTitle,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var body =
			$"Your album \"{albumTitle}\" was rejected by platform moderation.\n\n{userMessage.Trim()}";
		return TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private async Task SendAlbumRemovedDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string albumTitle,
		string userMessage,
		CancellationToken cancellationToken)
	{
		var body =
			$"Your album \"{albumTitle}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private static string BuildMediaRemovedMessage(string itemTitle, string albumTitle, string userMessage) =>
		$"An item \"{itemTitle}\" was removed from your album \"{albumTitle}\".\n\n{TruncateUserMessage(userMessage)}";

	private static string ResolveMediaTitle(AlbumMedia media)
	{
		if (!string.IsNullOrWhiteSpace(media.Title))
			return media.Title.Trim();
		var kind = media.MediaType == MediaTypeEnum.Video ? "Video" : "Photo";
		return $"{kind} #{media.SortOrder + 1}";
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
					"Platform DM after album action failed: {ErrorCode} operator={OperatorId} creator={CreatorId}",
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
				"Platform DM after album action threw; operator={OperatorId} creator={CreatorId}",
				operatorUserId,
				creatorId);
		}
	}
}
