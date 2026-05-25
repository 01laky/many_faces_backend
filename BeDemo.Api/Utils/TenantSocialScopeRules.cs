using BeDemo.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Utils;

/// <summary>
/// ACL A19: tenant directory / messaging — same participation rule as <c>UsersController.GetUsers</c>
/// (users must have <see cref="Models.UserFaceProfile"/> for the scoped face).
/// </summary>
public static class TenantSocialScopeRules
{
	/// <summary>
	/// True when both users have a face profile row for <paramref name="faceId"/>.
	/// </summary>
	public static async Task<bool> BothUsersParticipateInFaceAsync(
		ApplicationDbContext context,
		int faceId,
		string userId1,
		string userId2,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(userId1) || string.IsNullOrEmpty(userId2))
			return false;

		async Task<bool> InFaceAsync(string userId) =>
			await context.UserProfiles.AnyAsync(
				up => up.UserId == userId &&
					  context.UserFaceProfiles.Any(ufp =>
						  ufp.UserProfileId == up.Id && ufp.FaceId == faceId),
				cancellationToken);

		return await InFaceAsync(userId1) && await InFaceAsync(userId2);
	}
}
