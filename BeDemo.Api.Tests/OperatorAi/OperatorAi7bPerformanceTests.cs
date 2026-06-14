using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Edge cases for the 7B performance optimizations (operator-ai-7b-performance-v1, PF-1…PF-20): the deterministic
/// count fast-path (O2), the single-bundle fast-path (O3), map sampling/cap (O7/O11), the streaming skill seam (O4),
/// embed-once (O8), the single-active-generation guard (O17), the answer cache (O18), and the decision helper (O19).
/// </summary>
public sealed class OperatorAi7bPerformanceTests
{
	// ── PF-1 — IsSimpleCountQuestion is strict ────────────────────────────────
	[Theory]
	[InlineData("how many users", true)]
	[InlineData("how many albums are pending", true)]
	[InlineData("total number of faces", true)]
	[InlineData("count of reels", true)]
	[InlineData("compare users vs albums", false)]
	[InlineData("user growth trend", false)]
	[InlineData("why are reels failing", false)]
	[InlineData("breakdown of albums by status", false)]
	[InlineData("average messages per day", false)]
	[InlineData("complete overview of the platform", false)]
	[InlineData("hello there", false)]
	public void PF1_IsSimpleCountQuestion_is_strict(string message, bool expected) =>
		OperatorAiStatsIntent.IsSimpleCountQuestion(message).Should().Be(expected);

	// ── PF-2 / PF-3 — deterministic count fast-path (0 generations) ────────────
	[Fact]
	public async Task PF2_count_fast_path_answers_deterministically_with_zero_generations()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetcher = PrefetcherFor(0, "{\"totalCount\":1234,\"byStatus\":{\"approved\":1000,\"pending\":234}}");
		var decisions = DecisionsCount(isCount: true);

		var plan = await Build(ai, prefetcher, decisions)
			.PrepareSelectedAsync("how many users", new[] { 0 }, 1, appendCoverageNote: false);

		plan.IsComplete.Should().BeTrue("a simple count over one bundle needs no LLM");
		plan.Trace.FastPath.Should().Be("count");
		plan.Trace.Generations.Should().Be(0);
		plan.CompleteAnswer.Should().Contain("1,234").And.Contain("1,000 approved").And.Contain("234 pending");
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task PF3_count_fast_path_does_not_fire_for_multi_bundle()
	{
		var ai = new Mock<IAiGrpcService>();
		AnyGenerate(ai, "mapped facts");
		var prefetcher = PrefetcherForMany(new[] { 0, 1 }, "{\"totalCount\":5}");
		var decisions = DecisionsCount(isCount: true); // even if "count", multi-bundle must not take the fast-path

		var plan = await Build(ai, prefetcher, decisions, synthesis: false)
			.PrepareSelectedAsync("how many users and albums", new[] { 0, 1 }, 2, appendCoverageNote: false);

		plan.Trace.FastPath.Should().Be("stitch");
		plan.IsComplete.Should().BeTrue("synthesis is off ⇒ the stitched draft is the answer");
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	[Fact]
	public async Task PF2b_count_fast_path_falls_through_when_no_totalCount()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetcher = PrefetcherFor(0, "{\"note\":\"no count here\"}");
		var decisions = DecisionsCount(isCount: true);

		var plan = await Build(ai, prefetcher, decisions)
			.PrepareSelectedAsync("how many users", new[] { 0 }, 1, appendCoverageNote: false);

		// No totalCount ⇒ the formatter returns null ⇒ fall to the single-bundle generation (bias to the LLM).
		plan.Trace.FastPath.Should().Be("single-bundle");
		plan.IsComplete.Should().BeFalse();
	}

	// ── PF-4 — single-bundle fast-path ────────────────────────────────────────
	[Fact]
	public async Task PF4_single_bundle_fast_path_streams_one_generation_no_synthesis()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetcher = PrefetcherFor(0, "{\"totalCount\":42}");
		var decisions = DecisionsCount(isCount: false); // not a count ⇒ single focused generation

		var plan = await Build(ai, prefetcher, decisions)
			.PrepareSelectedAsync("tell me about albums", new[] { 0 }, 1, appendCoverageNote: false);

		plan.Trace.FastPath.Should().Be("single-bundle");
		plan.IsComplete.Should().BeFalse("the single-bundle answer is generated (streamed), not pre-computed");
		plan.StreamPrompt.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task PF4b_multi_bundle_uses_synthesis_when_enabled()
	{
		var ai = new Mock<IAiGrpcService>();
		AnyGenerate(ai, "facts");
		var prefetcher = PrefetcherForMany(new[] { 0, 1 }, "{\"totalCount\":5}");
		var decisions = DecisionsCount(isCount: false);

		var plan = await Build(ai, prefetcher, decisions, synthesis: true)
			.PrepareSelectedAsync("compare albums and reels", new[] { 0, 1 }, 2, appendCoverageNote: false);

		plan.Trace.FastPath.Should().Be("synthesis");
		plan.StreamPrompt.Should().NotBeNullOrEmpty("the synthesis is the terminal generation");
		plan.FallbackText.Should().NotBeEmpty("the stitched draft is the graceful fallback");
	}

	// ── PF-8 — map step uses the 96-token cap + low temperature + stop ─────────
	[Fact]
	public async Task PF8_map_generation_uses_token_cap_temperature_and_stops()
	{
		var ai = new Mock<IAiGrpcService>();
		int? capTokens = null;
		double? capTemp = null;
		IReadOnlyList<string>? capStops = null;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int t, string? __, string? ___, double? temp, IReadOnlyList<string>? stops, string? ____, CancellationToken _____) =>
				{ capTokens = t; capTemp = temp; capStops = stops; return "facts"; });
		var prefetcher = PrefetcherForMany(new[] { 0, 1 }, "{\"totalCount\":5}");
		var decisions = DecisionsCount(isCount: false);

		await Build(ai, prefetcher, decisions, synthesis: false, mapTokens: 96, mapTemp: 0.2)
			.PrepareSelectedAsync("two areas", new[] { 0, 1 }, 2, appendCoverageNote: false);

		capTokens.Should().Be(96, "the per-bundle map cap is O7's 96 tokens");
		capTemp.Should().Be(0.2, "the map uses O11's low temperature");
		capStops.Should().NotBeNull().And.NotBeEmpty("the map passes O11 stop sequences");
	}

	// ── PF-2c — the count formatter is deterministic + thousands-formatted ─────
	[Fact]
	public void PF2c_count_formatter_formats_numbers_and_breakdown()
	{
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(0);
		var line = OperatorAiCountFastPath.TryFormat(meta, "{\"totalCount\":12345,\"byStatus\":{\"approved\":12000,\"pending\":345}}");
		line.Should().NotBeNull();
		line!.Should().Contain("12,345 total").And.Contain("12,000 approved").And.Contain("345 pending");

		OperatorAiCountFastPath.TryFormat(meta, "{\"no\":1}").Should().BeNull("no totalCount ⇒ defer to the LLM");
		OperatorAiCountFastPath.TryFormat(meta, "not json").Should().BeNull();
	}

	// ── PF-16 / O17 — single-active-generation guard ──────────────────────────
	[Fact]
	public void PF16_single_active_generation_guard_blocks_second_concurrent_turn()
	{
		var guard = new OperatorAiActiveGenerationGuard();
		guard.TryBegin(5).Should().BeTrue("first turn for a conversation reserves it");
		guard.TryBegin(5).Should().BeFalse("a second concurrent turn for the same conversation is rejected");
		guard.TryBegin(6).Should().BeTrue("a different conversation is unaffected");
		guard.End(5);
		guard.TryBegin(5).Should().BeTrue("after the first turn ends the conversation is free again");
	}

	// ── PF-17 / O18 — optional answer cache ───────────────────────────────────
	[Fact]
	public void PF17_answer_cache_disabled_by_default_then_hits_when_enabled()
	{
		using var mc = new MemoryCache(new MemoryCacheOptions());
		var off = new OperatorAiAnswerCache(mc, Options.Create(new OperatorAiOptions { AnswerCacheEnabled = false }));
		off.Set("stats", "how many users", "1,234 users");
		off.TryGet("stats", "how many users", out _).Should().BeFalse("the cache is off by default");

		using var mc2 = new MemoryCache(new MemoryCacheOptions());
		var on = new OperatorAiAnswerCache(mc2, Options.Create(new OperatorAiOptions { AnswerCacheEnabled = true, AnswerCacheTtlSeconds = 30 }));
		on.Set("stats", "how many users", "1,234 users");
		on.TryGet("stats", "how many users", out var hit).Should().BeTrue("an identical repeat hits");
		hit.Should().Be("1,234 users");
		on.TryGet("stats", "how many albums", out _).Should().BeFalse("a different question misses");
		on.Clear();
		on.TryGet("stats", "how many users", out _).Should().BeFalse("Clear() (reindex) invalidates everything");
	}

	// ── PF-18 / O19 — decision helper falls back to the deterministic heuristic ─
	[Fact]
	public async Task PF18_decision_helper_uses_deterministic_heuristic_when_helper_unset()
	{
		var ai = new Mock<IAiGrpcService>(MockBehavior.Strict); // must NOT call the model when no helper is configured
		var helper = new OperatorAiDecisionHelper(
			ai.Object,
			Options.Create(new AiServiceOptions { HelperModel = null }),
			Options.Create(new OperatorAiOptions { HelperForDecisions = true }),
			NullLogger<OperatorAiDecisionHelper>.Instance);

		helper.HelperEnabled.Should().BeFalse();
		(await helper.IsSimpleCountAsync("how many users")).Should().BeTrue();
		(await helper.IsSimpleCountAsync("why is moderation slow")).Should().BeFalse();
		(await helper.DetectReportTypeAsync("moderation backlog report")).Should().Be("moderation_backlog");
	}

	// ── PF-5/6 — StatsSkill streaming forwards deltas, falls back on error ─────
	[Fact]
	public async Task PF5_stats_skill_streams_deltas_then_final_answer()
	{
		var (skill, _) = StreamingStatsSkill(new[] { 0 }, "{\"totalCount\":9}", isCount: false,
			streamDeltas: new[] { "Albums: ", "9 total." }, streamError: false);

		var chunks = new List<OperatorAiStreamChunk>();
		await foreach (var c in skill.RunStreamingAsync(Req("tell me about albums"), default))
			chunks.Add(c);

		chunks.Where(c => !c.IsFinal).Select(c => c.Delta).Should().ContainInOrder("Albums: ", "9 total.");
		var final = chunks.Last();
		final.IsFinal.Should().BeTrue();
		final.FinalAnswer.Should().Be("Albums: 9 total.");
		final.Trace!.FastPath.Should().Be("single-bundle");
	}

	[Fact]
	public async Task PF6_stats_skill_streaming_falls_back_on_worker_error()
	{
		var (skill, _) = StreamingStatsSkill(new[] { 0 }, "{\"totalCount\":9}", isCount: false,
			streamDeltas: Array.Empty<string>(), streamError: true);

		var chunks = new List<OperatorAiStreamChunk>();
		await foreach (var c in skill.RunStreamingAsync(Req("tell me about albums"), default))
			chunks.Add(c);

		var final = chunks.Last();
		final.IsFinal.Should().BeTrue();
		final.FinalAnswer.Should().NotBeNullOrEmpty("a mid-stream error degrades to the fallback text, never an empty message");
	}

	[Fact]
	public async Task PF5b_stats_skill_zero_hit_streams_the_refusal()
	{
		var retriever = new Mock<IOperatorAiRetriever>();
		retriever.Setup(r => r.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(Array.Empty<int>(), OperatorAiSelectionStrategy.ZeroHit,
				Array.Empty<OperatorAiRetrievalHit>(), false, false, 0, 0));
		var skill = new StatsSkill(retriever.Object, Mock.Of<IOperatorAiLiveStatsOrchestrator>(), Mock.Of<IAiGrpcService>(), Mock.Of<IOperatorAiDecisionHelper>());

		var chunks = new List<OperatorAiStreamChunk>();
		await foreach (var c in skill.RunStreamingAsync(Req("weather?"), default))
			chunks.Add(c);

		chunks.Last().FinalAnswer.Should().Be(StatsSkill.ZeroHitRefusal);
	}

	// ── helpers ───────────────────────────────────────────────────────────────
	private static OperatorAiSkillRequest Req(string m) => new(m, 1, Array.Empty<ChatHistoryEntry>(), 1, 1.0);

	private static OperatorAiLiveStatsOrchestrator Build(
		Mock<IAiGrpcService> ai,
		Mock<IOperatorAiLiveStatsPrefetcher> prefetcher,
		Mock<IOperatorAiDecisionHelper> decisions,
		bool synthesis = true,
		int mapTokens = 96,
		double mapTemp = 0.2)
	{
		var options = Options.Create(new OperatorAiOptions
		{
			MaxParallelBundleAiCalls = 2,
			LiveUseAiSynthesisStitch = synthesis,
			LiveBundleMaxNewTokens = mapTokens,
			MapTemperature = mapTemp,
			OverallTurnBudgetMs = 10000,
			PerBundleGenerateTimeoutMs = 5000,
		});
		return new OperatorAiLiveStatsOrchestrator(prefetcher.Object, ai.Object, decisions.Object, options, Options.Create(new AiServiceOptions()), NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
	}

	private static void AnyGenerate(Mock<IAiGrpcService> ai, string result) =>
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(result);

	private static Mock<IOperatorAiDecisionHelper> DecisionsCount(bool isCount)
	{
		var d = new Mock<IOperatorAiDecisionHelper>();
		d.Setup(x => x.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isCount);
		return d;
	}

	private static OperatorAiBundleCacheEntry Ready(int index, string json)
	{
		var id = OperatorAiEntityBundleCatalog.GetByIndex(index).Id;
		return new OperatorAiBundleCacheEntry(index, id, OperatorAiBundleCacheState.Ready, json, null, DateTime.UtcNow, DateTime.UtcNow);
	}

	private static Mock<IOperatorAiLiveStatsPrefetcher> PrefetcherFor(int index, string json) =>
		PrefetcherForMany(new[] { index }, json);

	private static Mock<IOperatorAiLiveStatsPrefetcher> PrefetcherForMany(int[] indices, string json)
	{
		var prefetcher = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetcher.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(idx.ToDictionary(i => i, i => Ready(i, json)), idx.Count, 0, idx.Count, 0));
		return prefetcher;
	}

	/// <summary>Build a StatsSkill whose orchestrator returns a single-bundle stream plan and whose AI streams the given deltas.</summary>
	private static (StatsSkill Skill, Mock<IAiGrpcService> Ai) StreamingStatsSkill(
		int[] indices, string json, bool isCount, string[] streamDeltas, bool streamError)
	{
		var retriever = new Mock<IOperatorAiRetriever>();
		retriever.Setup(r => r.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(indices, OperatorAiSelectionStrategy.Rag,
				Array.Empty<OperatorAiRetrievalHit>(), false, false, 1, 1));

		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateStreamAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(StreamAsync(streamDeltas, streamError));

		var decisions = DecisionsCount(isCount);
		var orchestrator = Build(ai, PrefetcherForMany(indices, json), decisions);
		return (new StatsSkill(retriever.Object, orchestrator, ai.Object, decisions.Object), ai);
	}

	private static async IAsyncEnumerable<AiGenerateDelta> StreamAsync(string[] deltas, bool error)
	{
		foreach (var d in deltas)
		{
			await Task.Yield();
			yield return new AiGenerateDelta(d, false, null, null, null);
		}
		if (error)
			yield return new AiGenerateDelta(null, true, null, "ollama offline", "stream_failed");
		else
			yield return new AiGenerateDelta(string.Empty, true, "stop", null, null);
	}
}
