using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

public interface IFaceModerationService
{
	Task<bool> IsUserBannedFromFaceAsync(string userId, int faceId, CancellationToken cancellationToken = default);

	bool IsUserGloballyBanned(ApplicationUser user);

	/// <summary>When scoped to a face, peer messenger/UGC should be blocked for this user.</summary>
	Task<bool> ShouldBlockPeerActivityInFaceAsync(string userId, int faceId, CancellationToken cancellationToken = default);
}
