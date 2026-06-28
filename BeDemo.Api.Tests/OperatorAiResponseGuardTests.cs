using BeDemo.Api.Hubs;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiResponseGuardTests
{
	// ── IsInfrastructureFailure — every D5 marker + the "Error:" prefix (the hub maps these to AiUnavailable) ──
	[Theory]
	[InlineData("Error: AI service unavailable (Unavailable)")]
	[InlineData("error: ai service is down")] // case-insensitive prefix
	[InlineData("<urlopen error [Errno 111] Connection refused>")]
	[InlineData("the worker reported errno 111")]
	[InlineData("ai service unavailable")]
	[InlineData("ai service timed out")]
	[InlineData("the AI service is currently unavailable")] // D5: model-narrated, intervening words
	[InlineData("the AI service is unavailable right now")]
	[InlineData("the AI service is temporarily unavailable")]
	[InlineData("I cannot provide additional details or counts from other entities")] // D5 hybrid tail
	[InlineData("public_stats_absolute_url is not set")]
	public void IsInfrastructureFailure_true_for_all_markers(string text) =>
		OperatorAiResponseGuard.IsInfrastructureFailure(text).Should().BeTrue();

	[Theory]
	[InlineData("There are 640 reels currently in the system.")]
	[InlineData("The platform talks to its workers over gRPC.")] // bare "grpc" must NOT trip (backend-refactor §5)
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void IsInfrastructureFailure_false_for_normal_or_empty(string? text) =>
		OperatorAiResponseGuard.IsInfrastructureFailure(text).Should().BeFalse();

	// ── IsTransientStatusMessage — loading placeholders (the hub maps these to ModelLoading) ──
	[Theory]
	[InlineData("⏳ AI model sa práve načítava do pamäte.")] // sk with diacritics
	[InlineData("model sa nacitava")] // sk without diacritics
	[InlineData("worker says MODEL_LOADING right now")] // the D15 loading sentinel marker
	[InlineData("AI služba nie je dostupná")]
	public void IsTransientStatusMessage_true_for_loading_markers(string text) =>
		OperatorAiResponseGuard.IsTransientStatusMessage(text).Should().BeTrue();

	[Theory]
	[InlineData("There are 640 reels currently in the system.")]
	[InlineData("")]
	[InlineData(null)]
	public void IsTransientStatusMessage_false_for_normal_or_empty(string? text) =>
		OperatorAiResponseGuard.IsTransientStatusMessage(text).Should().BeFalse();

	// ── The two orchestrator sentinels classify to the right hub bucket ──
	[Fact]
	public void ModelLoadingSentinel_classifies_as_transient_not_infrastructure()
	{
		// D15 — the hub checks IsTransientStatusMessage FIRST, so the loading sentinel becomes ModelLoading.
		OperatorAiResponseGuard.IsTransientStatusMessage(OperatorAiLiveStatsOrchestrator.ModelLoadingSentinel).Should().BeTrue();
		OperatorAiResponseGuard.ShouldNotPersist(OperatorAiLiveStatsOrchestrator.ModelLoadingSentinel).Should().BeTrue();
	}

	[Fact]
	public void AllBundlesFailedSentinel_classifies_as_infrastructure()
	{
		OperatorAiResponseGuard.IsInfrastructureFailure(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel).Should().BeTrue();
		OperatorAiResponseGuard.ShouldNotPersist(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel).Should().BeTrue();
	}

	[Fact]
	public void ToUserFacingMessage_null_or_empty_becomes_placeholder()
	{
		OperatorAiResponseGuard.ToUserFacingMessage(null).Should().Be("...");
		OperatorAiResponseGuard.ToUserFacingMessage("   ").Should().Be("...");
	}

	[Fact]
	public void ToUserFacingMessage_transient_passes_through_trimmed()
	{
		OperatorAiResponseGuard.ToUserFacingMessage("  ⏳ AI model sa načítava  ").Should().Be("⏳ AI model sa načítava");
	}

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
