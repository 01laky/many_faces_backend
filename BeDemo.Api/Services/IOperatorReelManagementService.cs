namespace BeDemo.Api.Services;

/// <summary>Super-admin reel hard-delete with audit + best-effort platform DM.</summary>
public interface IOperatorReelManagementService
{
	/// <summary>Hard-deletes reel and cascaded rows. Returns true when done (including idempotent missing reel).</summary>
	Task<bool> HardDeleteReelAsync(
		string operatorUserId,
		int reelId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	/// <summary>Best-effort platform DM after moderation reject (reel still exists).</summary>
	Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string reelTitle,
		string userMessage,
		CancellationToken cancellationToken = default);
}
