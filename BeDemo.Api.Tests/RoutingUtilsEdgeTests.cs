using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public sealed class RoutingUtilsEdgeTests
{
	[Theory]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("/api/oauth2/token", true)]
	[InlineData("/api/auth/login", true)]
	[InlineData("/swagger/index.html", true)]
	[InlineData("/api/uploads/serve", true)]
	[InlineData("/api/profile/me", true)]
	[InlineData("/api/my/content-submissions", true)]
	[InlineData("/uploads/avatar.png", false)]
	[InlineData("/basic/api/albums", false)]
	public void IsExemptFromFaceScope_ShouldMatchExpected(string? path, bool expected)
	{
		Routing.IsExemptFromFaceScope(path).Should().Be(expected);
	}

	[Theory]
	[InlineData("/api/users", true)]
	[InlineData("/api/profile/me", false)]
	[InlineData("/api/oauth2/token", false)]
	[InlineData("/hubs/chat", true)]
	[InlineData("/basic/hubs/chat", false)]
	[InlineData(null, false)]
	public void IsReservedPathWithoutFacePrefix_ShouldMatchExpected(string? path, bool expected)
	{
		Routing.IsReservedPathWithoutFacePrefix(path).Should().Be(expected);
	}

	[Theory]
	[InlineData("", "")]
	[InlineData("BasicFace", "basic-face")]
	[InlineData("already-kebab", "already-kebab")]
	public void ConvertToKebabCase_ShouldNormalize(string input, string expected)
	{
		Routing.ConvertToKebabCase(input).Should().Be(expected);
	}
}
