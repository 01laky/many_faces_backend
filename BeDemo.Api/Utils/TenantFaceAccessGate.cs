using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Services;

namespace BeDemo.Api.Utils;

/// <summary>
/// Tenant isolation helper (ACL A6): non–platform operators must only act on the face from the URL prefix.
/// Returns <see cref="NotFoundObjectResult"/> so ID enumeration across tenants stays opaque (same as inline <c>GateTenantFaceOrNotFound</c>).
/// </summary>
public static class TenantFaceAccessGate
{
	/// <summary>
	/// When <paramref name="callerCanManageAllFaces"/> is false and <paramref name="targetFaceId"/> ≠ scoped face, blocks with 404.
	/// </summary>
	public static IActionResult? TryBlockTenantCrossFace(
		IFaceScopeContext faceScope,
		bool callerCanManageAllFaces,
		int targetFaceId)
	{
		if (callerCanManageAllFaces)
			return null;
		if (targetFaceId != faceScope.FaceId)
			return new NotFoundObjectResult(new { error = "Face not found" });
		return null;
	}
}
