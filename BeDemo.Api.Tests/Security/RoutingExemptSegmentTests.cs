using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Security;

/// <summary>
/// Regression tests for the segment-aware face-scope exemption (backend-refactor §2 Security fix): a path that merely
/// shares a string prefix with an exempt route must NOT inherit the exemption and bypass tenant enforcement.
/// </summary>
public sealed class RoutingExemptSegmentTests
{
	[Theory]
	[InlineData("/api/profile")]            // exact
	[InlineData("/api/profile/avatar")]     // real sub-path
	[InlineData("/api/oauth2/token")]
	[InlineData("/api/my/submissions")]
	[InlineData("/api/uploads/serve")]
	[InlineData("/swagger/index.html")]
	[InlineData("/openapi/v1.json")]
	[InlineData("/favicon.ico")]            // static file under a non-/api prefix
	public void Genuinely_exempt_paths_are_exempt(string path) =>
		Routing.IsExemptFromFaceScope(path).Should().BeTrue();

	[Theory]
	[InlineData("/api/profile-evil")]       // prefix-confusion — must NOT be exempt
	[InlineData("/api/profiles")]
	[InlineData("/api/mystuff")]            // not "/api/my/..."
	[InlineData("/api/oauth2x/token")]
	[InlineData("/api/uploadsX")]
	[InlineData("/public/api/albums")]      // face-prefixed tenant route
	[InlineData(null)]
	[InlineData("")]
	public void Prefix_confusable_or_tenant_paths_are_not_exempt(string? path) =>
		Routing.IsExemptFromFaceScope(path).Should().BeFalse();
}
