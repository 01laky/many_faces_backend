namespace BeDemo.Api.Security;

/// <summary>
/// Custom JWT claim types for BeDemo (thin access tokens, ACL J6).
/// </summary>
public static class BeDemoClaimTypes
{
	/// <summary>
	/// Access-token session version; must match <see cref="Models.ApplicationUser.AccessTokenVersion"/> or the request is rejected.
	/// </summary>
	public const string AccessTokenVersion = "atv";
}
