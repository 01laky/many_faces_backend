namespace BeDemo.Api.Services;

/// <summary>Super-admin story hard-delete and per-image removal (stories are outside content-moderation queue).</summary>
public interface IOperatorStoryManagementService
{
	/// <summary>Hard-delete story when targeted on <paramref name="faceId"/>; idempotent when missing or wrong face.</summary>
	Task HardDeleteStoryAsync(
		string operatorUserId,
		int storyId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	/// <summary>Remove one story image; returns false when story/image missing or not on face.</summary>
	Task<bool> DeleteStoryImageAsync(
		string operatorUserId,
		int storyId,
		int imageId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);
}
