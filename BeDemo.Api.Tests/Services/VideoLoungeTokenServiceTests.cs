using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Services;

/// <summary>
/// Characterization tests for <see cref="VideoLoungeTokenService"/> (backend-refactor §4.6, currently 0 tests): the
/// stub-vs-signed branch, the TTL clamp, and the join-mode → publish-grant mapping. Documents the current behaviour;
/// the Listener-can-publish-video case is flagged for the Phase 1 security fix.
/// </summary>
public sealed class VideoLoungeTokenServiceTests
{
	private static VideoLoungeTokenService Svc(VideoLoungeOptions o) => new(Options.Create(o));

	private static VideoLoungeOptions Real(int ttl = 10) => new()
	{
		UseStubTokens = false,
		ApiKey = "test-key",
		ApiSecret = "this-is-a-test-secret-at-least-32-bytes-long!!",
		LiveKitUrl = "wss://livekit.test",
		TokenTtlMinutes = ttl,
	};

	[Fact]
	public void Stub_token_when_UseStubTokens_true()
	{
		var r = Svc(new VideoLoungeOptions { UseStubTokens = true }).CreateToken(5, "u1", "User One", VideoLoungeJoinMode.Full);
		r.IsStub.Should().BeTrue();
		r.Token.Should().StartWith("vl-stub.");
		r.RoomName.Should().Be("vl_session_5");
		r.ServerUrl.Should().Be("wss://stub.livekit.local");
	}

	[Fact]
	public void Falls_back_to_stub_when_keys_missing_even_if_stub_disabled()
	{
		var r = Svc(new VideoLoungeOptions { UseStubTokens = false, ApiKey = "", ApiSecret = "" })
			.CreateToken(1, "u", "n", VideoLoungeJoinMode.Viewer);
		r.IsStub.Should().BeTrue("no real keys ⇒ must never emit an unsigned 'real' token");
	}

	[Fact]
	public void Signed_token_ttl_is_clamped_to_5_60_minutes()
	{
		var below = Svc(Real(ttl: 1)).CreateToken(1, "u", "n", VideoLoungeJoinMode.Full);
		var above = Svc(Real(ttl: 999)).CreateToken(1, "u", "n", VideoLoungeJoinMode.Full);
		below.IsStub.Should().BeFalse();
		(below.ExpiresAtUtc - DateTime.UtcNow).TotalMinutes.Should().BeApproximately(5, 1);
		(above.ExpiresAtUtc - DateTime.UtcNow).TotalMinutes.Should().BeApproximately(60, 1);
	}

	[Theory]
	[InlineData(VideoLoungeJoinMode.Viewer, false)]
	[InlineData(VideoLoungeJoinMode.AdminStealth, false)]
	[InlineData(VideoLoungeJoinMode.Listener, true)]
	[InlineData(VideoLoungeJoinMode.Full, true)]
	public void Signed_grant_canPublish_follows_join_mode(VideoLoungeJoinMode mode, bool expectedCanPublish)
	{
		var r = Svc(Real()).CreateToken(9, "u", "n", mode);
		using var grant = JsonDocument.Parse(ReadVideoGrant(r.Token));
		grant.RootElement.GetProperty("canPublish").GetBoolean().Should().Be(expectedCanPublish);
		grant.RootElement.GetProperty("canSubscribe").GetBoolean().Should().BeTrue();
		grant.RootElement.GetProperty("room").GetString().Should().Be("vl_session_9");
	}

	[Theory]
	// §4.6 fix: a Listener is audio-only — its grant must NOT include "camera"; only Full may publish video.
	[InlineData(VideoLoungeJoinMode.Listener, false)]
	[InlineData(VideoLoungeJoinMode.Full, true)]
	public void Listener_cannot_publish_video_only_Full_can(VideoLoungeJoinMode mode, bool expectCamera)
	{
		var r = Svc(Real()).CreateToken(9, "u", "n", mode);
		using var grant = JsonDocument.Parse(ReadVideoGrant(r.Token));
		var sources = grant.RootElement.GetProperty("canPublishSources").EnumerateArray().Select(e => e.GetString()).ToList();
		sources.Should().Contain("microphone");
		(sources.Contains("camera")).Should().Be(expectCamera, "only a Full participant may publish video");
	}

	[Theory]
	[InlineData(VideoLoungeJoinMode.Viewer)]
	[InlineData(VideoLoungeJoinMode.AdminStealth)]
	public void Non_publishers_get_no_publishable_sources(VideoLoungeJoinMode mode)
	{
		var r = Svc(Real()).CreateToken(9, "u", "n", mode);
		using var grant = JsonDocument.Parse(ReadVideoGrant(r.Token));
		grant.RootElement.GetProperty("canPublishSources").EnumerateArray().Should().BeEmpty();
	}

	private static string ReadVideoGrant(string jwt) =>
		new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.First(c => c.Type == "video").Value;
}
