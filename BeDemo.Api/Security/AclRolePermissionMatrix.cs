namespace BeDemo.Api.Security;

/// <summary>
/// Static role → capability narrative for ACL A5. Runtime enforcement remains in controllers + <see cref="AclPermissionKeys"/> via
/// <see cref="Services.AccessCapabilitiesService"/>; this type documents intended product semantics for agents and reviewers.
/// </summary>
/// <remarks>
/// <para><b>Global roles</b> (JWT <c>ClaimTypes.Role</c> from <c>UserRoles</c> / <c>ApplicationUser.UserRoleId</c>):</para>
/// <list type="bullet">
/// <item><description><c>SUPER_ADMIN</c> — same platform gates as ADMIN plus capability <c>platform:super</c>; use for break-glass ops only.</description></item>
/// <item><description><c>ADMIN</c> — platform UI on admin face: all faces, users, global PageTypes mutations, delegated face roles.</description></item>
/// <item><description><c>USER</c> — default registered user; tenant APIs + self-service face role within whitelist.</description></item>
/// <item><description><c>HOST</c> — global host role (rare); not the same as per-face <c>FACE_HOST</c> (see <see cref="BeDemo.Api.Utils.FaceRoleParticipation"/> / <c>UserRole</c> A8).</description></item>
/// </list>
/// <para><b>Face roles</b> (<c>UserFaceRole</c> for URL-scoped face):</para>
/// <list type="bullet">
/// <item><description><c>FACE_ADMIN</c> — tenant power user; not hidden in face-roles API for platform admins; self-assign blocked without platform admin.</description></item>
/// <item><description><c>FACE_USER</c>, <c>INZERENT</c>, <c>SUBSCRIBER</c> — normal participation; self-service allowed.</description></item>
/// <item><description><c>FACE_HOST</c> — directory / landing semantics (<see cref="BeDemo.Api.Utils.FaceRoleParticipation"/>); default after OAuth2 register on all faces.</description></item>
/// </list>
/// </remarks>
public static class AclRolePermissionMatrix
{
    // Intentionally no runtime API: documentation-only anchor for A5; keep in sync with docs/guides/acl-and-capabilities.md (monorepo root).
}
