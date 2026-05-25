/*
 * IFaceScopeContext.cs — read-only view of the current HTTP face scope for controllers and services.
 *
 * Populated from HttpContext.Items by RoutingMiddleware. On exempt paths (OAuth, Swagger, …)
 * <see cref="IsAvailable"/> is false — controllers on those routes must not depend on face scope.
 */

namespace BeDemo.Api.Services;

/// <summary>
/// Per-request face scope resolved from the URL prefix.
/// </summary>
public interface IFaceScopeContext
{
	/// <summary>
	/// True when this request went through face routing and Items were set.
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// Database id of the face from the URL prefix. Only valid when <see cref="IsAvailable"/>.
	/// </summary>
	int FaceId { get; }

	/// <summary>
	/// <see cref="Models.Face.Index"/> for the scoped face.
	/// </summary>
	string FaceIndex { get; }

	/// <summary>
	/// Whether the scoped face allows anonymous access (maps to <see cref="Models.Face.IsPublic"/>).
	/// </summary>
	bool IsPublicFace { get; }

	/// <summary>
	/// True when operating under the seeded admin scope face (cross-tenant admin UI).
	/// </summary>
	bool IsAdminFaceScope { get; }

	/// <summary>
	/// For data queries that accept an optional <c>faceId</c> in admin scope: returns the requested
	/// tenant face id when present and valid in query; otherwise the scoped <see cref="FaceId"/>.
	/// For non-admin scope, always returns <see cref="FaceId"/> (callers should ignore user-supplied duplicates).
	/// </summary>
	int ResolveDataFaceId(int? queryFaceId);
}
