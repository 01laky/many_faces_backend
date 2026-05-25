namespace BeDemo.Api.Services;

/// <summary>Super-admin album hard-delete and per-media delete with audit + best-effort platform DM.</summary>
public interface IOperatorAlbumManagementService
{
	/// <summary>Hard-deletes album and cascaded rows. Returns true when done (including idempotent missing album).</summary>
	Task<bool> HardDeleteAlbumAsync(
		string operatorUserId,
		int albumId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	/// <summary>Deletes one media row. Returns false when album/media not found for face.</summary>
	Task<bool> DeleteAlbumMediaAsync(
		string operatorUserId,
		int albumId,
		int mediaId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	/// <summary>Best-effort platform DM after moderation reject (album still exists).</summary>
	Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string albumTitle,
		string userMessage,
		CancellationToken cancellationToken = default);
}
