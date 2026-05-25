namespace BeDemo.Api.Models;

/// <summary>
/// Who can discover / read face content (profiles list, profile detail baseline).
/// IsPublic on Face remains separate (route/auth gate for the app shell).
/// </summary>
public enum FaceVisibility
{
	/// <summary>Visible broadly (anonymous when combined with public face flows).</summary>
	Public = 0,

	/// <summary>Authenticated users only.</summary>
	Private = 1,

	/// <summary>Users who have a UserFaceProfile row for this face.</summary>
	Face = 2,

	/// <summary>Hidden from normal users; face admin and global admins only.</summary>
	Hidden = 3,
}
