using BeDemo.Api.Hubs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Hubs;

/// <summary>
/// Regression test for the backend-refactor §5 fix: a legitimate assistant answer that merely mentions "gRPC" must
/// NOT be classified as an infrastructure failure (the bare "grpc" marker was a false-positive that refused real
/// answers). Genuine transport failures still surface via the "Error:" prefix / "ai service unavailable" markers.
/// </summary>
public sealed class OperatorAiResponseGuardGrpcMarkerTests
{
	[Theory]
	[InlineData("The platform talks to its workers over gRPC.")]
	[InlineData("Our gRPC services include search, push and mailer.")]
	public void Answers_that_mention_grpc_are_not_infrastructure_failures(string text) =>
		OperatorAiResponseGuard.IsInfrastructureFailure(text).Should().BeFalse();

	[Theory]
	[InlineData("Error: AI service unavailable (Unavailable)")]
	[InlineData("<urlopen error [Errno 111] Connection refused>")]
	[InlineData("ai service timed out")]
	public void Real_transport_failures_are_still_detected(string text) =>
		OperatorAiResponseGuard.IsInfrastructureFailure(text).Should().BeTrue();
}
