using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai degraded failure-handling — the shared "is this generation result an error sentinel, not an answer"
/// check. The gRPC client returns "Error: …" / "AI support is currently disabled …" as a STRING on failure; both the
/// per-bundle map path and the terminal generation rely on this so the convention lives in one place.
/// </summary>
public sealed class OperatorAiGenerationErrorsTests
{
	[Theory]
	[InlineData("Error: AI service unavailable (Unavailable)", true)]
	[InlineData("  Error: timed out", true)] // leading whitespace tolerated
	[InlineData("AI support is currently disabled for this system.", true)]
	[InlineData("There are 640 reels in the system.", false)]
	[InlineData("", false)]
	[InlineData("   ", false)]
	[InlineData(null, false)]
	public void IsErrorText_detects_the_grpc_error_sentinel(string? text, bool expected) =>
		OperatorAiGenerationErrors.IsErrorText(text).Should().Be(expected);

	[Theory]
	[InlineData("Error: x", true)]
	[InlineData("", true)]
	[InlineData("   ", true)]
	[InlineData(null, true)]
	[InlineData("real answer", false)]
	public void IsUnusable_is_error_or_empty(string? text, bool expected) =>
		OperatorAiGenerationErrors.IsUnusable(text).Should().Be(expected);
}
