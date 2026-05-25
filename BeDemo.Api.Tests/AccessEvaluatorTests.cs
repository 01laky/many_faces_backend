using System.Security.Claims;
using FluentAssertions;
using Moq;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>ACL A3: <see cref="IAccessEvaluator"/> is a thin DI wrapper — behaviour must stay aligned with <see cref="BeDemo.Api.Utils.PlatformAccessRules"/>.</summary>
public class AccessEvaluatorTests
{
	[Fact]
	public void CanManageAllFaces_Delegates_To_PlatformRules()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.IsAdminFaceScope).Returns(true);
		var ev = new AccessEvaluator(scope.Object);
		var super = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.SuperAdmin)], "test"));
		var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.Admin)], "test"));
		ev.CanManageAllFaces(super).Should().BeTrue();
		ev.CanMutateGlobalPageTypes(super).Should().BeTrue();
		ev.CanManageAllFaces(admin).Should().BeFalse();
		ev.CanMutateGlobalPageTypes(admin).Should().BeFalse();
	}

	[Fact]
	public void CanManageAllFaces_False_On_Tenant_Scope()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.IsAdminFaceScope).Returns(false);
		var ev = new AccessEvaluator(scope.Object);
		var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.Admin)], "test"));
		ev.CanManageAllFaces(admin).Should().BeFalse();
	}

	[Fact]
	public void IsGlobalSuperAdmin_True_Only_For_SuperRole()
	{
		var scope = new Mock<IFaceScopeContext>();
		var ev = new AccessEvaluator(scope.Object);
		var super = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.SuperAdmin)], "test"));
		var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.Admin)], "test"));
		ev.IsGlobalSuperAdmin(super).Should().BeTrue();
		ev.IsGlobalSuperAdmin(admin).Should().BeFalse();
	}
}
