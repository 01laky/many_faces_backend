using System.Security.Claims;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.Security;

/// <summary>
/// Characterization + behaviour tests for the X5 declarative authorization policies. These pin that the policies
/// resolve and that <see cref="ManageAllFacesAuthorizationHandler"/> reproduces the imperative
/// <see cref="Utils.PlatformAccessRules.CanManageAllFaces"/> gate exactly (admin face scope AND global super-admin),
/// so a controller can later swap its in-body check for <c>[Authorize(Policy = …)]</c> with identical semantics.
/// </summary>
public sealed class PlatformAuthorizationPoliciesTests
{
	private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
	{
		var identity = new ClaimsIdentity(
			roles.Select(r => new Claim(ClaimTypes.Role, r)),
			authenticationType: "test");
		return new ClaimsPrincipal(identity);
	}

	private static IFaceScopeContext FaceScope(bool isAdminScope)
	{
		var mock = new Mock<IFaceScopeContext>();
		mock.SetupGet(s => s.IsAdminFaceScope).Returns(isAdminScope);
		return mock.Object;
	}

	private static async Task<bool> EvaluateManageAllFacesAsync(IFaceScopeContext faceScope, ClaimsPrincipal user)
	{
		var requirement = new ManageAllFacesRequirement();
		var handler = new ManageAllFacesAuthorizationHandler(faceScope);
		var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
		await handler.HandleAsync(context);
		return context.HasSucceeded;
	}

	// ---- ManageAllFaces handler: admin scope + super-admin only -------------------------------------------------

	[Fact]
	public async Task ManageAllFaces_SuperAdmin_InAdminScope_Succeeds()
	{
		var succeeded = await EvaluateManageAllFacesAsync(
			FaceScope(isAdminScope: true),
			PrincipalWithRoles(UserRole.GlobalRoleNames.SuperAdmin));

		succeeded.Should().BeTrue();
	}

	[Fact]
	public async Task ManageAllFaces_SuperAdmin_OutsideAdminScope_Fails()
	{
		var succeeded = await EvaluateManageAllFacesAsync(
			FaceScope(isAdminScope: false),
			PrincipalWithRoles(UserRole.GlobalRoleNames.SuperAdmin));

		succeeded.Should().BeFalse();
	}

	[Fact]
	public async Task ManageAllFaces_PlainAdmin_InAdminScope_Fails()
	{
		// Global ADMIN is NOT super-admin — the operator gate must reject it even in admin scope.
		var succeeded = await EvaluateManageAllFacesAsync(
			FaceScope(isAdminScope: true),
			PrincipalWithRoles(UserRole.GlobalRoleNames.Admin));

		succeeded.Should().BeFalse();
	}

	[Fact]
	public async Task ManageAllFaces_OrdinaryUser_Fails()
	{
		var succeeded = await EvaluateManageAllFacesAsync(
			FaceScope(isAdminScope: true),
			PrincipalWithRoles(UserRole.GlobalRoleNames.User));

		succeeded.Should().BeFalse();
	}

	// ---- Policy registration smoke ------------------------------------------------------------------------------

	[Theory]
	[InlineData(PlatformAuthorizationPolicies.SuperAdmin)]
	[InlineData(PlatformAuthorizationPolicies.GlobalAdmin)]
	[InlineData(PlatformAuthorizationPolicies.ManageAllFaces)]
	public async Task Configure_RegistersAllNamedPolicies(string policyName)
	{
		var services = new ServiceCollection();
		services.AddAuthorization(PlatformAuthorizationPolicies.Configure);
		var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

		var policy = await provider.GetPolicyAsync(policyName);

		policy.Should().NotBeNull("policy '{0}' must be registered so controllers can reference it by name", policyName);
	}

	[Fact]
	public async Task SuperAdminPolicy_RequireAssertion_MatchesIsGlobalSuperAdmin()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddAuthorization(PlatformAuthorizationPolicies.Configure);
		var provider = services.BuildServiceProvider();
		var authService = provider.GetRequiredService<IAuthorizationService>();

		var superAdmin = PrincipalWithRoles(UserRole.GlobalRoleNames.SuperAdmin);
		var plainUser = PrincipalWithRoles(UserRole.GlobalRoleNames.User);

		(await authService.AuthorizeAsync(superAdmin, resource: null, PlatformAuthorizationPolicies.SuperAdmin))
			.Succeeded.Should().BeTrue();
		(await authService.AuthorizeAsync(plainUser, resource: null, PlatformAuthorizationPolicies.SuperAdmin))
			.Succeeded.Should().BeFalse();
	}

	[Fact]
	public async Task GlobalAdminPolicy_AcceptsAdminAndSuperAdmin_RejectsUser()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddAuthorization(PlatformAuthorizationPolicies.Configure);
		var provider = services.BuildServiceProvider();
		var authService = provider.GetRequiredService<IAuthorizationService>();

		foreach (var role in new[] { UserRole.GlobalRoleNames.Admin, UserRole.GlobalRoleNames.SuperAdmin })
		{
			(await authService.AuthorizeAsync(PrincipalWithRoles(role), resource: null, PlatformAuthorizationPolicies.GlobalAdmin))
				.Succeeded.Should().BeTrue("global admin policy must accept {0}", role);
		}

		(await authService.AuthorizeAsync(PrincipalWithRoles(UserRole.GlobalRoleNames.User), resource: null, PlatformAuthorizationPolicies.GlobalAdmin))
			.Succeeded.Should().BeFalse();
	}
}
