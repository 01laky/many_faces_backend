using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class FaceVisibilityAccess
{
	public static async Task<bool> CanViewFaceProfileContentAsync(
		ApplicationDbContext context,
		Face face,
		string? viewerUserId,
		CancellationToken ct = default)
	{
		switch (face.Visibility)
		{
			case FaceVisibility.Public:
				return true;
			case FaceVisibility.Hidden:
				return await IsFaceOrGlobalAdminAsync(context, face.Id, viewerUserId, ct);
			case FaceVisibility.Private:
				return !string.IsNullOrEmpty(viewerUserId);
			case FaceVisibility.Face:
				if (string.IsNullOrEmpty(viewerUserId))
					return false;
				var profileId = await context.UserProfiles.AsNoTracking()
					.Where(up => up.UserId == viewerUserId)
					.Select(up => up.Id)
					.FirstOrDefaultAsync(ct);
				if (profileId == 0)
					return false;
				return await context.UserFaceProfiles.AsNoTracking()
					.AnyAsync(ufp => ufp.FaceId == face.Id && ufp.UserProfileId == profileId, ct);
			default:
				return false;
		}
	}

	public static async Task<bool> IsFaceOrGlobalAdminAsync(
		ApplicationDbContext context,
		int faceId,
		string? userId,
		CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(userId))
			return false;

		var user = await context.Users
			.AsNoTracking()
			.Include(u => u.UserRole)
			.FirstOrDefaultAsync(u => u.Id == userId, ct);
		if (user?.UserRole == null)
			return false;

		var n = user.UserRole.Name;
		if (string.Equals(n, UserRole.GlobalRoleNames.SuperAdmin, StringComparison.Ordinal) ||
			string.Equals(n, UserRole.GlobalRoleNames.Admin, StringComparison.Ordinal))
			return true;

		var faceAdminRole = await context.UserRoles.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceAdmin && r.Scope == RoleScope.Face, ct);
		if (faceAdminRole == null)
			return false;

		return await context.UserFaceRoles.AsNoTracking()
			.AnyAsync(
				ufr => ufr.UserId == userId && ufr.FaceId == faceId && ufr.UserRoleId == faceAdminRole.Id,
				ct);
	}
}
