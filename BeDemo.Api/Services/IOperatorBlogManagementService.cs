namespace BeDemo.Api.Services;

/// <summary>Super-admin blog hard-delete and per-image removal with platform DMs.</summary>
public interface IOperatorBlogManagementService
{
	Task<bool> HardDeleteBlogAsync(
		string operatorUserId,
		int blogId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	Task<bool> DeleteBlogImageAsync(
		string operatorUserId,
		int blogId,
		int imageId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string blogTitle,
		string userMessage,
		CancellationToken cancellationToken = default);
}
