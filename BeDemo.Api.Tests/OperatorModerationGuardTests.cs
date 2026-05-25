using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public class OperatorModerationGuardTests
{
	[Fact]
	public void CanBanTarget_ShouldRejectSelfAndSuperAdmin()
	{
		var target = new ApplicationUser
		{
			Id = "target",
			UserRole = new UserRole { Name = UserRole.GlobalRoleNames.User },
		};
		OperatorModerationGuard.CanBanTarget("target", target).Should().BeFalse();
		var superTarget = new ApplicationUser
		{
			Id = "target",
			UserRole = new UserRole { Name = UserRole.GlobalRoleNames.SuperAdmin },
		};
		OperatorModerationGuard.CanBanTarget("operator", superTarget).Should().BeFalse();
		OperatorModerationGuard.CanBanTarget("operator", target).Should().BeTrue();
	}

	[Fact]
	public void CanChangeFaceRole_ShouldRejectSuperAdminTarget()
	{
		var super = new ApplicationUser
		{
			UserRole = new UserRole { Name = UserRole.GlobalRoleNames.SuperAdmin },
		};
		var user = new ApplicationUser
		{
			UserRole = new UserRole { Name = UserRole.GlobalRoleNames.User },
		};
		OperatorModerationGuard.CanChangeFaceRole(super).Should().BeFalse();
		OperatorModerationGuard.CanChangeFaceRole(user).Should().BeTrue();
	}

	[Fact]
	public void IsGlobalSuperAdminRole_ShouldMatchCaseInsensitive()
	{
		OperatorModerationGuard.IsGlobalSuperAdminRole("SUPER_ADMIN").Should().BeTrue();
		OperatorModerationGuard.IsGlobalSuperAdminRole("super_admin").Should().BeTrue();
		OperatorModerationGuard.IsGlobalSuperAdminRole("ADMIN").Should().BeFalse();
	}
}
