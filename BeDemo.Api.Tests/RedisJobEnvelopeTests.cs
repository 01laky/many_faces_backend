using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class RedisJobEnvelopeTests
{
	[Fact]
	public void RedisJobEnvelope_ShouldRoundTripJson()
	{
		var env = new RedisJobEnvelope("abc", "reel.postprocess", "{\"reelId\":1}");
		var json = JsonSerializer.Serialize(env);
		var back = JsonSerializer.Deserialize<RedisJobEnvelope>(json);
		back.Should().NotBeNull();
		back!.Id.Should().Be("abc");
		back.Type.Should().Be("reel.postprocess");
		back.Payload.Should().Contain("reelId");
	}
}
