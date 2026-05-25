using BeDemo.Api.Hubs;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiResponseGuardTests
{
	[Fact]
	public void IsInfrastructureFailure_detects_urlopen_errors()
	{
		OperatorAiResponseGuard.IsInfrastructureFailure("<urlopen error [Errno 111] Connection refused>")
			.Should()
			.BeTrue();
	}

	[Fact]
	public void ShouldNotPersist_includes_transient_loading()
	{
		OperatorAiResponseGuard.ShouldNotPersist("⏳ AI model sa práve načítava do pamäte.").Should().BeTrue();
	}

	[Fact]
	public void ToUserFacingMessage_strips_assistant_name_prefix()
	{
		OperatorAiResponseGuard.ToUserFacingMessage("MFAI Assistant: Ahoj, ako pomôžem?")
			.Should()
			.Be("Ahoj, ako pomôžem?");
	}

	[Fact]
	public void ToUserFacingMessage_maps_infrastructure_to_sk()
	{
		var msg = OperatorAiResponseGuard.ToUserFacingMessage("Error: Connection refused");
		msg.Should().Contain("Ospravedlňujem sa");
		msg.Should().NotContain("Connection refused");
	}
}
