using System.Security.Claims;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <inheritdoc />
public sealed class AccessEvaluator : IAccessEvaluator
{
	private readonly IFaceScopeContext _faceScope;

	public AccessEvaluator(IFaceScopeContext faceScope) => _faceScope = faceScope;

	/// <inheritdoc />
	public bool CanManageAllFaces(ClaimsPrincipal user) =>
		PlatformAccessRules.CanManageAllFaces(_faceScope, user);

	/// <inheritdoc />
	public bool CanMutateGlobalPageTypes(ClaimsPrincipal user) =>
		PlatformAccessRules.CanMutateGlobalPageTypes(_faceScope, user);

	/// <inheritdoc />
	public bool IsGlobalAdmin(ClaimsPrincipal user) =>
		PlatformAccessRules.IsGlobalAdmin(user);

	/// <inheritdoc />
	public bool IsGlobalSuperAdmin(ClaimsPrincipal user) =>
		PlatformAccessRules.IsGlobalSuperAdmin(user);
}
