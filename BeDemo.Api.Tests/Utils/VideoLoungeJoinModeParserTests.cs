using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the video-lounge join-mode parser (previously untested): only the three
/// member modes parse, the operator-stealth mode is rejected as a member mode, and nullish/garbage fail.
/// </summary>
public sealed class VideoLoungeJoinModeParserTests
{
	[Theory]
	[InlineData("viewer", VideoLoungeJoinMode.Viewer)]
	[InlineData("VIEWER", VideoLoungeJoinMode.Viewer)]
	[InlineData("  Listener  ", VideoLoungeJoinMode.Listener)]
	[InlineData("full", VideoLoungeJoinMode.Full)]
	public void TryParseMemberMode_accepts_member_modes(string value, VideoLoungeJoinMode expected)
	{
		var ok = VideoLoungeJoinModeParser.TryParseMemberMode(value, out var mode);
		ok.Should().BeTrue();
		mode.Should().Be(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("garbage")]
	[InlineData("adminstealth")]
	public void TryParseMemberMode_rejects_nullish_garbage_and_operator_stealth(string? value)
	{
		VideoLoungeJoinModeParser.TryParseMemberMode(value, out _).Should().BeFalse();
	}

	[Fact]
	public void IsOperatorStealth_only_true_for_admin_stealth()
	{
		VideoLoungeJoinModeParser.IsOperatorStealth(VideoLoungeJoinMode.AdminStealth).Should().BeTrue();
		VideoLoungeJoinModeParser.IsOperatorStealth(VideoLoungeJoinMode.Viewer).Should().BeFalse();
		VideoLoungeJoinModeParser.IsOperatorStealth(VideoLoungeJoinMode.Full).Should().BeFalse();
	}
}
