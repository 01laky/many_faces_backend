using Serilog;

namespace BeDemo.Api.Utils;

/// <summary>
/// Structured security audit events (BSH3-L2) — password/role changes that invalidate sessions.
/// </summary>
public static class SecurityAuditLogger
{
	/// <summary>Logs when a user's password hash changes (tokens revoked via ATV bump).</summary>
	public static void LogPasswordChanged(string targetUserId) =>
		Log.Information(
			"SECURITY_AUDIT event=password_changed targetUserId={TargetUserId}",
			targetUserId);

	/// <summary>Logs when a user's global role changes (tokens revoked via ATV bump).</summary>
	public static void LogGlobalRoleChanged(string targetUserId, int previousRoleId, int newRoleId) =>
		Log.Information(
			"SECURITY_AUDIT event=global_role_changed targetUserId={TargetUserId} previousRoleId={PreviousRoleId} newRoleId={NewRoleId}",
			targetUserId,
			previousRoleId,
			newRoleId);
}
