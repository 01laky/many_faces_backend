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
	[InlineData("  Error: timed out", true)] // leading spaces tolerated
	[InlineData("\tError: tab-led", true)] // leading tab tolerated (TrimStart trims all whitespace)
	[InlineData("\n  Error: newline-led", true)] // leading newline tolerated
	[InlineData("error: lowercase prefix", true)] // case-insensitive prefix
	[InlineData("ERROR: SHOUTING", true)]
	[InlineData("AI support is currently disabled for this system.", true)]
	[InlineData("ai support is currently disabled", true)] // case-insensitive marker
	[InlineData("There are 640 reels in the system.", false)]
	// Boundary: the marker must be a PREFIX, not merely contained — a real answer that mentions an error word is fine.
	[InlineData("The import finished with no Error: lines in the log.", false)]
	[InlineData("This data set is currently disabled by the operator.", false)] // "disabled" not at start
	[InlineData("", false)]
	[InlineData("   ", false)]
	[InlineData("\t\n", false)] // whitespace-only of any kind
	[InlineData(null, false)]
	public void IsErrorText_detects_the_grpc_error_sentinel(string? text, bool expected) =>
		OperatorAiGenerationErrors.IsErrorText(text).Should().Be(expected);

	[Theory]
	[InlineData("Error: x", true)]
	[InlineData("error: lowercase", true)]
	[InlineData("AI support is currently disabled", true)]
	[InlineData("", true)]
	[InlineData("   ", true)]
	[InlineData("\t\n ", true)]
	[InlineData(null, true)]
	[InlineData("real answer", false)]
	[InlineData("The log had no Error: lines.", false)] // contains-not-prefix is a usable answer
	public void IsUnusable_is_error_or_empty(string? text, bool expected) =>
		OperatorAiGenerationErrors.IsUnusable(text).Should().Be(expected);
}
