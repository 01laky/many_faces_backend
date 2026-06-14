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
	private static OperatorAiLiveStatsOrchestrator Build(Mock<IAiGrpcService> ai, Mock<IOperatorAiLiveStatsPrefetcher> prefetcher, int perBundleTimeoutMs = 1000, int overallBudgetMs = 10000, bool synthesisStitch = false, string? helperModel = null)
	{
		var options = Options.Create(new OperatorAiOptions
		{
			MaxParallelBundleAiCalls = 2,
			BroadOverviewMaxParallel = 4,
			PerBundleGenerateTimeoutMs = perBundleTimeoutMs,
			OverallTurnBudgetMs = overallBudgetMs,
			// Off by default so most tests assert the deterministic stitch; the broad tests turn it ON to prove the
			// broad path forces the deterministic stitch even when synthesis is enabled.
			LiveUseAiSynthesisStitch = synthesisStitch,
		});
		// 7B-perf: decision helper stubbed to "never a simple count" so these tests exercise the map/stitch path,
		// not the deterministic count fast-path (covered by its own dedicated tests).
		var decisions = new Mock<IOperatorAiDecisionHelper>();
		decisions.Setup(d => d.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		return new OperatorAiLiveStatsOrchestrator(prefetcher.Object, ai.Object, decisions.Object, options, Options.Create(new AiServiceOptions { HelperModel = helperModel }), NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
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

	// ── Full-stats broad-overview (per-bundle map → deterministic stitch) ─────────────────────────────────

	[Fact]
	public async Task Broad_overview_uses_deterministic_stitch_even_when_synthesis_is_enabled()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("Stat line.");

		// Synthesis ON, but broadOverview must force the deterministic stitch: 3 per-bundle maps and NO synthesis
		// Generate — so the model is never asked to enumerate all entities in one (truncatable) generation.
		var result = await Build(ai, PrefetcherReturningReady(), synthesisStitch: true)
			.RunWithSelectedIndicesAsync("give me full statistics", new[] { 0, 1, 2 }, 2, appendCoverageNote: true, broadOverview: true);

		result.Should().Contain("Full platform snapshot", "broad-overview answers carry the full-snapshot note (§3.5)");
		ai.Verify(
			a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
			Times.Exactly(3),
			"exactly the 3 per-bundle maps run — the deterministic stitch adds NO synthesis Generate");
	}

	[Fact]
	public async Task Broad_overview_routes_all_maps_to_the_helper_model()
	{
		var models = new List<string?>();
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns((string _, int _, string? _, string? _, double? _, IReadOnlyList<string>? _, string? model, CancellationToken _) =>
			{
				lock (models) models.Add(model);
				return Task.FromResult("Stat line.");
			});

		await Build(ai, PrefetcherReturningReady(), helperModel: "qwen-helper")
			.RunWithSelectedIndicesAsync("all statistics", new[] { 0, 1, 2 }, 2, appendCoverageNote: true, broadOverview: true);

		models.Should().HaveCount(3).And.OnlyContain(m => m == "qwen-helper", "all broad maps run on the CPU helper");
	}

	[Fact]
	public async Task Broad_overview_falls_back_to_default_model_when_no_helper_configured()
	{
		var models = new List<string?>();
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns((string _, int _, string? _, string? _, double? _, IReadOnlyList<string>? _, string? model, CancellationToken _) =>
			{
				lock (models) models.Add(model);
				return Task.FromResult("Stat line.");
			});

		await Build(ai, PrefetcherReturningReady(), helperModel: null)
			.RunWithSelectedIndicesAsync("all statistics", new[] { 0, 1 }, 2, appendCoverageNote: true, broadOverview: true);

		models.Should().OnlyContain(m => m == null, "with no helper configured the maps use the worker default model (7B)");
	}

	[Fact]
	public async Task Broad_overview_reports_partial_when_a_bundle_fails()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(async (string prompt, int _, string? statsCtx, string? _, double? _, IReadOnlyList<string>? _, string? _, CancellationToken ct) =>
			{
				if ((prompt + statsCtx).Contains("SLOWMARKER"))
					await Task.Delay(3000, ct); // bundle 12 times out → one Failed part
				return "Stat line.";
			});

		var result = await Build(ai, PrefetcherReturningReady(), perBundleTimeoutMs: 1000, overallBudgetMs: 15000)
			.RunWithSelectedIndicesAsync("full statistics", new[] { 0, 12 }, 2, appendCoverageNote: true, broadOverview: true);

		result.Should().Contain("Full platform snapshot", "the broad note still renders");
		result.Should().Contain("temporarily unavailable", "a failed bundle is reported as partial coverage, not silently dropped");
	}
}
