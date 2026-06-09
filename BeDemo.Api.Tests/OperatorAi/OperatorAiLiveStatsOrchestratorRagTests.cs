using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Edge cases for the unified orchestrator's RAG entry point <see cref="OperatorAiLiveStatsOrchestrator.RunWithSelectedIndicesAsync"/>
/// (§6/§17.7, RT-20). Verifies: empty selection returns empty (the caller emits the zero-hit refusal); the
/// broad-overview coverage note; and that a per-bundle Generate timeout degrades just that section while the turn
/// still completes (never hangs, never empty-errors). AI synthesis is disabled so the deterministic stitch is
/// asserted directly. Mocks the prefetcher (no DB) and the embedder/generator.
/// </summary>
public sealed class OperatorAiLiveStatsOrchestratorRagTests
{
	private static OperatorAiLiveStatsOrchestrator Build(Mock<IAiGrpcService> ai, Mock<IOperatorAiLiveStatsPrefetcher> prefetcher, int perBundleTimeoutMs = 1000, int overallBudgetMs = 10000)
	{
		var options = Options.Create(new OperatorAiOptions
		{
			MaxParallelBundleAiCalls = 2,
			PerBundleGenerateTimeoutMs = perBundleTimeoutMs,
			OverallTurnBudgetMs = overallBudgetMs,
			LiveUseAiSynthesisStitch = false, // assert the deterministic stitch, not a synthesized rewrite
		});
		// 7B-perf: decision helper stubbed to "never a simple count" so these tests exercise the map/stitch path,
		// not the deterministic count fast-path (covered by its own dedicated tests).
		var decisions = new Mock<IOperatorAiDecisionHelper>();
		decisions.Setup(d => d.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		return new OperatorAiLiveStatsOrchestrator(prefetcher.Object, ai.Object, decisions.Object, options, Options.Create(new AiServiceOptions()), NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
	}

	private static OperatorAiBundleCacheEntry ReadyEntry(int index)
	{
		// Bundle 12's payload carries a marker so the Generate mock can make just that one bundle time out.
		var json = index == 12 ? "{\"v\":\"SLOWMARKER\"}" : "{\"v\":\"FAST\"}";
		var id = OperatorAiEntityBundleCatalog.GetByIndex(index).Id;
		return new OperatorAiBundleCacheEntry(index, id, OperatorAiBundleCacheState.Ready, json, null, DateTime.UtcNow, DateTime.UtcNow);
	}

	private static Mock<IOperatorAiLiveStatsPrefetcher> PrefetcherReturningReady()
	{
		var prefetcher = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetcher.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(idx.ToDictionary(i => i, ReadyEntry), idx.Count, 0, idx.Count, 0));
		return prefetcher;
	}

	[Fact]
	public async Task Empty_selection_returns_empty_string()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetcher = new Mock<IOperatorAiLiveStatsPrefetcher>();

		var result = await Build(ai, prefetcher).RunWithSelectedIndicesAsync("hi", Array.Empty<int>(), 2, appendCoverageNote: false);

		result.Should().BeEmpty("the orchestrator only maps+stitches; the zero-hit refusal is the caller's job");
		prefetcher.Verify(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Coverage_note_is_appended_for_broad_overview()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("Users: 1,234 total.");

		// Two indices ⇒ the multi-bundle map/stitch path (the single-bundle fast-path, O3, intentionally skips the
		// stitch + coverage note since there is nothing to stitch across).
		var result = await Build(ai, PrefetcherReturningReady())
			.RunWithSelectedIndicesAsync("platform overview", new[] { 0, 1 }, 2, appendCoverageNote: true);

		result.Should().Contain("most relevant data areas", "broad-overview answers note the top-K coverage (§6)");
	}

	[Fact]
	public async Task Per_bundle_timeout_degrades_one_section_and_turn_still_completes()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(async (string prompt, int _, string? statsCtx, string? __, double? _t, IReadOnlyList<string>? _s, string? _m, CancellationToken ct) =>
			{
				// The slow bundle (carrying SLOWMARKER) blocks past the per-bundle timeout → it is dropped.
				if ((prompt + statsCtx).Contains("SLOWMARKER"))
					await Task.Delay(3000, ct);
				return "FASTANSWER";
			});

		var sw = System.Diagnostics.Stopwatch.StartNew();
		var result = await Build(ai, PrefetcherReturningReady(), perBundleTimeoutMs: 1000, overallBudgetMs: 15000)
			.RunWithSelectedIndicesAsync("how many users and chat messages?", new[] { 0, 12 }, 2, appendCoverageNote: false);
		sw.Stop();

		result.Should().NotBeNullOrEmpty("a per-bundle timeout must not fail the whole turn (RT-20)");
		result.Should().Contain("FASTANSWER", "the bundle that answered in time survives");
		sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the turn returns soon after the per-bundle timeout, not after the slow Generate");
	}
}
