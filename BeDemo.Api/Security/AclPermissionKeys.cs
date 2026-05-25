namespace BeDemo.Api.Security;

/// <summary>
/// Canonical permission strings for <see cref="Controllers.MeController"/> capabilities (D.8). Single catalog — no magic strings in FE for these keys.
/// </summary>
public static class AclPermissionKeys
{
	public const string PlatformSuper = "platform:super";

	/// <summary>Admin face scope + global SuperAdmin: platform operator APIs.</summary>
	public const string PlatformAdmin = "platform:admin";

	/// <summary>Create/update/delete <see cref="Models.PageType"/> rows.</summary>
	public const string PlatformPagetypeMutate = "platform:pagetype:mutate";

	/// <summary>Authenticated request with resolved face scope (URL prefix).</summary>
	public const string TenantSession = "tenant:session";

	/// <summary>User has a <see cref="Models.UserFaceRole"/> on the scoped face.</summary>
	public const string FaceMember = "face:member";

	/// <summary>May call self-service face role picker (subset of roles + PUT my-role whitelist).</summary>
	public const string FaceRoleSelfService = "face:role:self-service";
}
