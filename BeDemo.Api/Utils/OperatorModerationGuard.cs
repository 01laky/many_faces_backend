using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class OperatorModerationGuard
{
	public static bool IsGlobalSuperAdminRole(string? roleName) =>
		string.Equals(roleName, UserRole.GlobalRoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase);

	public static bool CanBanTarget(string operatorUserId, ApplicationUser target)
	{
		if (string.Equals(operatorUserId, target.Id, StringComparison.Ordinal))
			return false;
		return !IsGlobalSuperAdminRole(target.UserRole?.Name);
	}

	public static bool CanChangeFaceRole(ApplicationUser target) =>
		!IsGlobalSuperAdminRole(target.UserRole?.Name);
}
