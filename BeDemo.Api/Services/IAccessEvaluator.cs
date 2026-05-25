using System.Security.Claims;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <summary>
/// Request-scoped ACL facade (ACL A3/A6): reads <see cref="IFaceScopeContext"/> + principal so controllers avoid duplicating
/// <c>admin face + global role</c> formulas. Heavy authorization stays in <see cref="PlatformAccessRules"/>; this type is the DI entry point.
/// </summary>
public interface IAccessEvaluator
{
	/// <inheritdoc cref="PlatformAccessRules.CanManageAllFaces"/>
	bool CanManageAllFaces(ClaimsPrincipal user);

	/// <inheritdoc cref="PlatformAccessRules.CanMutateGlobalPageTypes"/>
	bool CanMutateGlobalPageTypes(ClaimsPrincipal user);

	/// <inheritdoc cref="PlatformAccessRules.IsGlobalAdmin"/>
	bool IsGlobalAdmin(ClaimsPrincipal user);

	/// <inheritdoc cref="PlatformAccessRules.IsGlobalSuperAdmin"/>
	bool IsGlobalSuperAdmin(ClaimsPrincipal user);
}
