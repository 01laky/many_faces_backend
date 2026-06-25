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
	public void ToUserFacingMessage_maps_infrastructure_to_english_without_removed_mode()
	{
		// After the RAG refactor (D10/D11) the infra-failure message is English and must not
		// reference the removed `inline` stats mode.
		var msg = OperatorAiResponseGuard.ToUserFacingMessage("Error: Connection refused");
		msg.Should().Contain("Sorry");
		msg.Should().NotContain("Connection refused");
		msg.Should().NotContain("inline");
	}

	[Theory]
	// operator-ai degraded-handling D5 — the demo-day hybrid (a real count + a model-narrated "AI unavailable"
	// apology) must NOT be persisted; the markers above missed it because of the intervening "is currently".
	[InlineData("The total number of system users in our application is 33. Unfortunately, the AI service is currently unavailable, so I cannot provide additional details or counts from other entities.")]
	[InlineData("Error: AI service unavailable (all bundle sections failed)")]
	public void ShouldNotPersist_catches_model_narrated_unavailability(string text)
	{
		OperatorAiResponseGuard.ShouldNotPersist(text).Should().BeTrue();
	}

	[Fact]
	public void ShouldNotPersist_passes_a_normal_grounded_answer()
	{
		OperatorAiResponseGuard.ShouldNotPersist("There are 640 reels currently in the system.").Should().BeFalse();
	}
}
