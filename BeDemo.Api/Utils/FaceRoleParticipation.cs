using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

/// <summary>
/// Face **participation** semantics (ACL A8): <see cref="UserRole.FaceRoleNames.FaceHost"/> marks a per-face “landing / not yet participating”
/// posture — <see cref="IsActiveForFaceRoleName"/> is false for hosts so directory UIs can treat them differently from
/// <c>FACE_USER</c> / <c>INZERENT</c> / etc. This is **orthogonal** to global <see cref="UserRole.GlobalRoleNames.Host"/> on
/// <see cref="ApplicationUser"/> (rare platform-level role, not wired the same as <c>FACE_HOST</c>).
/// </summary>
public static class FaceRoleParticipation
{
	public static bool IsHostFaceRole(string? roleName) =>
		string.Equals(roleName, UserRole.FaceRoleNames.FaceHost, StringComparison.Ordinal);

	/// <summary>Non-host face roles count as “active” participants in face-directory UX.</summary>
	public static bool IsActiveForFaceRoleName(string? roleName) => !IsHostFaceRole(roleName);
}
