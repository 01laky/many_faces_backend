/*
 * FaceScopeConstants.cs — central constants for multi-tenant (face) HTTP scope.
 *
 * Every business API request is expected under a URL prefix derived from Face.Index
 * (kebab-case), e.g. /basic/api/Stories. The routing middleware resolves that prefix
 * to a face row and stores the result in HttpContext.Items using the keys below.
 *
 * The special seeded face with Index = "admin" is the only scope where cross-face
 * administration APIs (e.g. listing or creating faces) are allowed. Tenant scopes
 * must never observe or mutate another face's data.
 */

namespace BeDemo.Api.Utils;

/// <summary>
/// Keys and well-known face indices for request-scoped face resolution.
/// </summary>
public static class FaceScopeConstants
{
	/// <summary>
	/// <see cref="HttpContext.Items"/> key: resolved database id of the face taken from the URL prefix.
	/// Set only by <c>RoutingMiddleware</c> after a successful prefix match; must not be set by client code.
	/// </summary>
	public const string RequestFaceIdItemKey = "BeDemo.RequestFaceId";

	/// <summary>
	/// <see cref="HttpContext.Items"/> key: <see cref="Face.Index"/> string for the resolved face (e.g. "basic", "admin").
	/// </summary>
	public const string RequestFaceIndexItemKey = "BeDemo.RequestFaceIndex";

	/// <summary>
	/// <see cref="HttpContext.Items"/> key: whether the resolved face allows anonymous access to scoped routes
	/// (maps to <see cref="Face.IsPublic"/>).
	/// </summary>
	public const string RequestFaceIsPublicItemKey = "BeDemo.RequestFaceIsPublic";

	/// <summary>
	/// <see cref="HttpContext.Items"/> key: true when the resolved face is the platform admin scope
	/// (<see cref="AdminFaceIndex"/>), not a tenant face.
	/// </summary>
	public const string RequestFaceIsAdminScopeItemKey = "BeDemo.RequestFaceIsAdminScope";

	/// <summary>
	/// Seeded <see cref="Face.Index"/> for the administration UI scope. Kebab-case URL prefix is "admin".
	/// </summary>
	public const string AdminFaceIndex = "admin";

	/// <summary>
	/// Returns true when <paramref name="faceIndex"/> identifies the admin scope face (case-insensitive).
	/// </summary>
	public static bool IsAdminFaceIndex(string? faceIndex) =>
		!string.IsNullOrEmpty(faceIndex) &&
		string.Equals(faceIndex, AdminFaceIndex, StringComparison.OrdinalIgnoreCase);
}
