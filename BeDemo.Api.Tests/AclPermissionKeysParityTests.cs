using FluentAssertions;
using Xunit;
using BeDemo.Api.Security;

namespace BeDemo.Api.Tests;

/// <summary>
/// **Cross-repo contract test:** backend constants in <see cref="AclPermissionKeys"/> must stay in lockstep
/// with the sorted union exported by both SPAs (<c>many_faces_portal</c>/<c>many_faces_admin</c> <c>src/acl/aclPermissionKeys.ts</c>,
/// symbol <c>ALL_ACL_PERMISSION_KEYS_SORTED</c>). If this fails, either update the TS catalog or add the new
/// permission server-side first — otherwise the UI will compile capabilities the API never issues.
/// </summary>
public sealed class AclPermissionKeysParityTests
{
	/// <summary>
	/// Order matches frontend <c>ALL_ACL_PERMISSION_KEYS_SORTED</c> for deterministic contract tests.
	/// </summary>
	[Fact]
	public void Sorted_catalog_matches_typescript_aclPermissionKeys()
	{
		var sorted = new[]
		{
			AclPermissionKeys.FaceMember,
			AclPermissionKeys.FaceRoleSelfService,
			AclPermissionKeys.PlatformAdmin,
			AclPermissionKeys.PlatformPagetypeMutate,
			AclPermissionKeys.PlatformSuper,
			AclPermissionKeys.TenantSession,
		};

		sorted.Should().Equal(
			"face:member",
			"face:role:self-service",
			"platform:admin",
			"platform:pagetype:mutate",
			"platform:super",
			"tenant:session");
	}
}
