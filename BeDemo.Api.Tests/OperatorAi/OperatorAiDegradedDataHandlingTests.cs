using BeDemo.Api.Configuration;
using BeDemo.Api.Hubs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai degraded failure-handling (Phase 1). When per-bundle generations fail the gRPC client returns an
/// "Error: …" STRING (not an exception); these tests pin that such a result is dropped as a Failed section (clean
/// "data unavailable" note, no error text leaking into the answer / synthesis), that an all-failed turn returns the
/// honest infrastructure sentinel (→ hub ephemeral, not a persisted wall), and that a partial failure adds a
/// deterministic coverage note. AI synthesis is OFF so the deterministic stitch is asserted directly.
/// </summary>
public sealed class OperatorAiDegradedDataHandlingTests
{
	private static OperatorAiLiveStatsOrchestrator Build(Mock<IAiGrpcService> ai)
	{
		var options = Options.Create(new OperatorAiOptions
		{
			MaxParallelBundleAiCalls = 2,
			PerBundleGenerateTimeoutMs = 2000,
			OverallTurnBudgetMs = 10000,
			LiveUseAiSynthesisStitch = false,
			LiveBroadDeterministicCounts = false,
		});
		var decisions = new Mock<IOperatorAiDecisionHelper>();
		decisions.Setup(d => d.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		var prefetcher = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetcher.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(
					idx.ToDictionary(i => i, i => new OperatorAiBundleCacheEntry(
						i, OperatorAiEntityBundleCatalog.GetByIndex(i).Id, OperatorAiBundleCacheState.Ready,
						"{\"totalCount\":5}", null, DateTime.UtcNow, DateTime.UtcNow)),
					idx.Count, 0, idx.Count, 0));
		return new OperatorAiLiveStatsOrchestrator(
			prefetcher.Object, ai.Object, decisions.Object, options,
			Options.Create(new AiServiceOptions()), NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
	}

	private static void Generates(Mock<IAiGrpcService> ai, Func<string, string> byPrompt) =>
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string prompt, int _, string? _, string? _, double? _, IReadOnlyList<string>? _, string? _, CancellationToken _) => byPrompt(prompt));

	[Fact]
	public async Task Per_bundle_error_string_is_dropped_and_never_leaks_into_the_answer()
	{
		var ai = new Mock<IAiGrpcService>();
		// index 0 = entity.users (succeeds); index 1 = entity.userProfiles (the worker returns the "Error:" string).
		Generates(ai, prompt => prompt.Contains("entity.userProfiles")
			? "Error: AI service unavailable (Unavailable)"
			: "Users: 5 total.");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("Users: 5 total.");
		result.Should().NotContain("Error:", "an error-string generation must never reach the user-facing answer");
		result.Should().Contain("Data unavailable", "the failed section degrades to a clean note");
		result.Should().Contain("1 of 2 data area(s)", "a partial failure adds a deterministic coverage note (D4)");
	}

	[Fact]
	public async Task All_bundles_failing_returns_the_honest_unavailable_sentinel_not_a_wall()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "Error: AI service unavailable (Unavailable)");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		result.Should().NotContain("Data unavailable", "all-failed returns one honest message, not a wall of per-bundle notes");
		OperatorAiResponseGuard.ShouldNotPersist(result).Should().BeTrue("the sentinel must route to the ephemeral path, not be persisted");
	}

	[Fact]
	public async Task Healthy_multi_bundle_turn_is_unchanged()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "ok facts.");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("ok facts.");
		result.Should().NotContain("Data unavailable");
		result.Should().NotContain("could not be loaded");
		OperatorAiResponseGuard.ShouldNotPersist(result).Should().BeFalse();
	}
}
