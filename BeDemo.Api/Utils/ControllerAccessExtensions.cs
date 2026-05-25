using System.Security.Claims;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Utils;

/// <summary>DRY tenant isolation gate for controllers (wraps <see cref="TenantFaceAccessGate"/>).</summary>
public static class ControllerAccessExtensions
{
	/// <summary>Tenant isolation gate — 404 when cross-face access is blocked.</summary>
	public static IActionResult? GateTenantFaceOrNotFound(
		this IFaceScopeContext faceScope,
		IAccessEvaluator access,
		ClaimsPrincipal user,
		int targetFaceId) =>
		TenantFaceAccessGate.TryBlockTenantCrossFace(
			faceScope,
			access.CanManageAllFaces(user),
			targetFaceId);
}
