using System.Security.Claims;
using FluentAssertions;
using Moq;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class PlatformAccessRulesTests
{
	private static ClaimsPrincipal Principal(params string[] roles)
	{
		var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
		claims.Add(new Claim(ClaimTypes.NameIdentifier, "u1"));
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
	}

	private static Mock<IFaceScopeContext> MockScope(bool available, bool adminScope, int faceId = 1)
	{
		var m = new Mock<IFaceScopeContext>();
		m.SetupGet(x => x.IsAvailable).Returns(available);
		m.SetupGet(x => x.IsAdminFaceScope).Returns(adminScope);
		m.SetupGet(x => x.FaceId).Returns(faceId);
		m.SetupGet(x => x.FaceIndex).Returns(adminScope ? "admin" : "public");
		return m;
	}

	[Fact]
	public void IsGlobalAdmin_IsTrue_ForAdminAndSuperAdminRoles()
	{
		PlatformAccessRules.IsGlobalAdmin(Principal(UserRole.GlobalRoleNames.User)).Should().BeFalse();
		PlatformAccessRules.IsGlobalAdmin(Principal(UserRole.GlobalRoleNames.Host)).Should().BeFalse();
		PlatformAccessRules.IsGlobalAdmin(Principal(UserRole.GlobalRoleNames.Admin)).Should().BeTrue();
		PlatformAccessRules.IsGlobalAdmin(Principal(UserRole.GlobalRoleNames.SuperAdmin)).Should().BeTrue();
	}

	[Fact]
	public void IsGlobalSuperAdmin_IsTrue_OnlyForSuperAdmin()
	{
		PlatformAccessRules.IsGlobalSuperAdmin(Principal(UserRole.GlobalRoleNames.Admin)).Should().BeFalse();
		PlatformAccessRules.IsGlobalSuperAdmin(Principal(UserRole.GlobalRoleNames.SuperAdmin)).Should().BeTrue();
	}

	[Fact]
	public void CanManageAllFaces_RequiresAdminScopeAndSuperAdmin()
	{
		var adminUser = Principal(UserRole.GlobalRoleNames.Admin);
		var superAdmin = Principal(UserRole.GlobalRoleNames.SuperAdmin);
		PlatformAccessRules.CanManageAllFaces(MockScope(true, true).Object, adminUser).Should().BeFalse();
		PlatformAccessRules.CanManageAllFaces(MockScope(true, true).Object, superAdmin).Should().BeTrue();
		PlatformAccessRules.CanManageAllFaces(MockScope(true, false).Object, superAdmin).Should().BeFalse();
		PlatformAccessRules.CanManageAllFaces(MockScope(true, true).Object, Principal(UserRole.GlobalRoleNames.User)).Should().BeFalse();
	}

	[Fact]
	public void CanMutateGlobalPageTypes_MatchesCanManageAllFaces()
	{
		var super = Principal(UserRole.GlobalRoleNames.SuperAdmin);
		var scope = MockScope(true, true);
		PlatformAccessRules.CanMutateGlobalPageTypes(scope.Object, super)
			.Should().Be(PlatformAccessRules.CanManageAllFaces(scope.Object, super));
	}

	internal static Moq.Mock<IFaceScopeContext> AdminFaceScope() => MockScope(true, true);
}
