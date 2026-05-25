using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

/// <summary>
/// Face roles a user may assign to themselves via PUT .../faces/&#123;id&#125;/my-role without platform admin (A16).
/// FACE_ADMIN is delegated only (admin UI / future promotion APIs).
/// </summary>
public static class FaceRoleSelfServiceRules
{
	private static readonly HashSet<string> SelfAssignableNormalized = new(StringComparer.OrdinalIgnoreCase)
	{
		UserRole.FaceRoleNames.FaceUser,
		UserRole.FaceRoleNames.Inzerent,
		UserRole.FaceRoleNames.Subscriber,
		UserRole.FaceRoleNames.FaceHost,
	};

	public static bool IsSelfAssignableFaceRoleName(string? roleName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
			return false;
		return SelfAssignableNormalized.Contains(roleName.Trim());
	}
}
