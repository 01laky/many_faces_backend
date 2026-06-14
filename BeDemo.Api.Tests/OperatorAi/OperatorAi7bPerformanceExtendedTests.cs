using BeDemo.Api.Configuration;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Extended / exhaustive edge cases for the 7B performance slice — the paths the first PF suite touches only lightly:
/// the count formatter across malformed/odd JSON, strict count-intent in Slovak + null/whitespace, the decision helper
/// with the model ENABLED (YES/NO parsing, exception + unparseable fallback, report-type labels), orchestrator
/// single-bundle-not-ready / all-failed / empty / Role-B helper-map routing, the answer cache key normalization, the
/// active-generation guard, the router warm path, the AI-availability guard streaming, and the StatsSkill non-streaming
/// terminal-plan branches.
/// </summary>
public sealed class OperatorAi7bPerformanceExtendedTests
{
	// ── OperatorAiCountFastPath — exhaustive formatter edges ───────────────────
	[Fact]
	public void Count_formatter_handles_string_total_and_byType_fallback()
	{
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(0);
		// totalCount as a JSON string is still read; byStatus absent ⇒ byType is used for the breakdown.
		var line = OperatorAiCountFastPath.TryFormat(meta, "{\"totalCount\":\"2500\",\"byType\":{\"image\":2000,\"video\":500}}");
		line.Should().NotBeNull();
		line!.Should().Contain("2,500 total").And.Contain("2,000 image").And.Contain("500 video");
	}

	[Theory]
	[InlineData("{\"totalCount\":0}", "0 total")]                                     // zero is a valid answer
	[InlineData("{\"totalCount\":7,\"byStatus\":{}}", "7 total")]                      // empty breakdown ⇒ no dash
	[InlineData("{\"totalCount\":9,\"byStatus\":{\"approved\":\"x\"}}", "9 total")]    // non-numeric breakdown skipped
	[InlineData("{\"totalCount\":1000000}", "1,000,000 total")]                        // thousands grouping
	public void Count_formatter_valid_totals(string json, string expectedFragment)
	{
		var line = OperatorAiCountFastPath.TryFormat(OperatorAiEntityBundleCatalog.GetByIndex(0), json);
		line.Should().NotBeNull();
		line!.Should().Contain(expectedFragment);
	}

	[Theory]
	[InlineData("{\"byStatus\":{\"a\":1}}")]   // no totalCount ⇒ defer to LLM
	[InlineData("{\"totalCount\":null}")]       // null total ⇒ defer
	[InlineData("{\"totalCount\":1.5}")]        // non-integer ⇒ defer (counts are whole)
	[InlineData("[1,2,3]")]                      // not an object
	[InlineData("not json at all")]             // unparseable
	[InlineData("")]                              // empty
	[InlineData("   ")]                            // whitespace
	public void Count_formatter_returns_null_when_not_a_clean_count(string? json)
	{
		OperatorAiCountFastPath.TryFormat(OperatorAiEntityBundleCatalog.GetByIndex(0), json).Should().BeNull();
	}

	[Fact]
	public void Count_formatter_label_is_titlecased_from_compound_id()
	{
		// Bundle ids may look like "albums" or "albums.byStatus" — the label is the leading segment, title-cased.
		var meta = OperatorAiEntityBundleCatalog.GetByIndex(0);
		var line = OperatorAiCountFastPath.TryFormat(meta, "{\"totalCount\":5}");
		line.Should().NotBeNull();
		line!.Should().StartWith("**");
		line.Should().MatchRegex(@"^\*\*[A-Z]"); // capitalised label
	}

	// ── IsSimpleCountQuestion — Slovak + null/whitespace + extra disqualifiers ──
	[Theory]
	[InlineData("koľko používateľov", true)]
	[InlineData("počet albumov", true)]
	[InlineData("kolko reelov spolu", true)]
	[InlineData("koľko používateľov pribudlo za týždeň", false)]   // trend disqualifier
	[InlineData("priemerný počet správ", false)]                      // average disqualifier (priemer not matched, but "average"? use english)
	[InlineData("how many users per face", false)]                    // "per " disqualifier
	[InlineData("how many users and why", false)]                     // "why" disqualifier
	[InlineData("list how many albums", false)]                       // "list" disqualifier
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("    ", false)]
	public void IsSimpleCountQuestion_extended(string? message, bool expected) =>
		OperatorAiStatsIntent.IsSimpleCountQuestion(message).Should().Be(expected);

	// ── OperatorAiDecisionHelper — model ENABLED ───────────────────────────────
	private static OperatorAiDecisionHelper Helper(Mock<IAiGrpcService> ai, bool helperForDecisions = true, string? model = "qwen2.5:3b") =>
		new(ai.Object,
			Options.Create(new AiServiceOptions { HelperModel = model }),
			Options.Create(new OperatorAiOptions { HelperForDecisions = helperForDecisions }),
			NullLogger<OperatorAiDecisionHelper>.Instance);

	private static void HelperReturns(Mock<IAiGrpcService> ai, string text) =>
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(text);

	[Fact]
	public void Helper_enabled_flag_requires_model_and_toggle()
	{
		var ai = new Mock<IAiGrpcService>();
		Helper(ai, helperForDecisions: true, model: "m").HelperEnabled.Should().BeTrue();
		Helper(ai, helperForDecisions: false, model: "m").HelperEnabled.Should().BeFalse();
		Helper(ai, helperForDecisions: true, model: null).HelperEnabled.Should().BeFalse();
		Helper(ai, helperForDecisions: true, model: "  ").HelperEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task Helper_count_yes_overrides_deterministic_no()
	{
		var ai = new Mock<IAiGrpcService>();
		HelperReturns(ai, "YES");
		// Deterministic would say false (qualitative), but the enabled helper confirms YES.
		(await Helper(ai).IsSimpleCountAsync("is the platform healthy")).Should().BeTrue();
	}

	[Fact]
	public async Task Helper_count_no_overrides_deterministic_yes()
	{
		var ai = new Mock<IAiGrpcService>();
		HelperReturns(ai, "no, this is nuanced");
		(await Helper(ai).IsSimpleCountAsync("how many users")).Should().BeFalse();
	}

	[Fact]
	public async Task Helper_count_unparseable_falls_back_to_deterministic()
	{
		var ai = new Mock<IAiGrpcService>();
		HelperReturns(ai, "maybe?");
		(await Helper(ai).IsSimpleCountAsync("how many users")).Should().BeTrue("unparseable ⇒ deterministic (which is true)");
		(await Helper(ai).IsSimpleCountAsync("why slow")).Should().BeFalse("unparseable ⇒ deterministic (which is false)");
	}

	[Fact]
	public async Task Helper_count_exception_falls_back_to_deterministic()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("worker down"));
		(await Helper(ai).IsSimpleCountAsync("how many users")).Should().BeTrue();
	}

	[Fact]
	public async Task Helper_count_uses_the_helper_model_and_low_sampling()
	{
		var ai = new Mock<IAiGrpcService>();
		string? usedModel = null; double? usedTemp = null;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int __, string? ___, string? ____, double? temp, IReadOnlyList<string>? _____, string? model, CancellationToken _6) =>
				{ usedModel = model; usedTemp = temp; return "YES"; });

		await Helper(ai, model: "qwen2.5:3b").IsSimpleCountAsync("how many users");

		usedModel.Should().Be("qwen2.5:3b", "the decision routes to the CPU helper model");
		usedTemp.Should().Be(0.0, "classification is deterministic");
	}

	[Theory]
	[InlineData("moderation_backlog", "moderation_backlog")]
	[InlineData("the answer is grid_completeness here", "grid_completeness")]
	[InlineData("NONE", null)] // helper says none ⇒ deterministic (which is null for this message)
	public async Task Helper_report_type_enabled(string helperReply, string? expected)
	{
		var ai = new Mock<IAiGrpcService>();
		HelperReturns(ai, helperReply);
		(await Helper(ai).DetectReportTypeAsync("some ambiguous text")).Should().Be(expected);
	}

	[Fact]
	public async Task Helper_report_type_exception_falls_back_to_deterministic()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TimeoutException());
		(await Helper(ai).DetectReportTypeAsync("moderation backlog report")).Should().Be("moderation_backlog");
	}

	// ── Orchestrator — single-bundle-not-ready / all-failed / empty / Role B ────
	[Fact]
	public async Task Orchestrator_empty_indices_returns_empty_trace()
	{
		var plan = await BuildOrch(new Mock<IAiGrpcService>(), Prefetch(Array.Empty<int>(), "{}"))
			.PrepareSelectedAsync("x", Array.Empty<int>(), 1, false);
		plan.IsComplete.Should().BeTrue();
		plan.CompleteAnswer.Should().BeEmpty();
		plan.Trace.FastPath.Should().Be("empty");
	}

	[Fact]
	public async Task Orchestrator_single_bundle_not_ready_falls_through_to_stitch()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetch = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetch.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(
					idx.ToDictionary(i => i, i => Failed(i)), 0, idx.Count, 0, idx.Count));

		var plan = await BuildOrch(ai, prefetch, synthesis: false, isCount: true)
			.PrepareSelectedAsync("how many users", new[] { 0 }, 1, false);

		// Not ready ⇒ neither count nor single-bundle fast-path; the stitch emits the deterministic "unavailable" note.
		plan.Trace.FastPath.Should().Be("stitch");
		plan.IsComplete.Should().BeTrue();
		plan.CompleteAnswer.Should().Contain("unavailable");
	}

	[Fact]
	public async Task Orchestrator_all_bundles_failed_still_returns_deterministic_text()
	{
		var ai = new Mock<IAiGrpcService>();
		var prefetch = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetch.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(idx.ToDictionary(i => i, i => Failed(i)), 0, idx.Count, 0, idx.Count));

		var plan = await BuildOrch(ai, prefetch, synthesis: false, isCount: false)
			.PrepareSelectedAsync("two areas", new[] { 0, 1 }, 2, false);

		plan.IsComplete.Should().BeTrue();
		plan.CompleteAnswer.Should().Contain("unavailable");
		plan.Trace.Generations.Should().Be(0, "no bundle produced an answer");
	}

	[Fact]
	public async Task Orchestrator_roleB_routes_odd_positions_to_helper_model()
	{
		var ai = new Mock<IAiGrpcService>();
		var models = new System.Collections.Concurrent.ConcurrentBag<string?>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int __, string? ___, string? ____, double? _5, IReadOnlyList<string>? _6, string? model, CancellationToken _7) =>
				{ models.Add(model); return "facts"; });

		// Role B ON + a helper model ⇒ even positions stay on the GPU 7B (null), odd positions go to the helper.
		var orch = BuildOrch(ai, Prefetch(new[] { 0, 1 }, "{\"totalCount\":3}"), synthesis: false, isCount: false,
			helperModel: "helper-3b", parallelMap: true);
		await orch.PrepareSelectedAsync("two areas", new[] { 0, 1 }, 2, false);

		models.Should().HaveCount(2);
		models.Should().Contain((string?)null);
		models.Should().Contain("helper-3b");
	}

	[Fact]
	public async Task Orchestrator_roleB_off_keeps_all_map_calls_on_default_model()
	{
		var ai = new Mock<IAiGrpcService>();
		var models = new System.Collections.Concurrent.ConcurrentBag<string?>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int __, string? ___, string? ____, double? _5, IReadOnlyList<string>? _6, string? model, CancellationToken _7) =>
				{ models.Add(model); return "facts"; });

		var orch = BuildOrch(ai, Prefetch(new[] { 0, 1 }, "{\"totalCount\":3}"), synthesis: false, isCount: false,
			helperModel: "helper-3b", parallelMap: false);
		await orch.PrepareSelectedAsync("two areas", new[] { 0, 1 }, 2, false);

		models.Should().OnlyContain(m => m == null, "Role B is off ⇒ no map call is routed to the helper");
	}

	// ── Answer cache — key normalization + isolation ───────────────────────────
	[Fact]
	public void Answer_cache_key_is_normalized_and_skill_scoped()
	{
		using var mc = new MemoryCache(new MemoryCacheOptions());
		var cache = new OperatorAiAnswerCache(mc, Options.Create(new OperatorAiOptions { AnswerCacheEnabled = true, AnswerCacheTtlSeconds = 60 }));

		cache.Set("stats", "How   Many  USERS", "1,234");
		cache.TryGet("stats", "how many users", out var hit).Should().BeTrue("whitespace + case are normalized");
		hit.Should().Be("1,234");
		cache.TryGet("reports", "how many users", out _).Should().BeFalse("a different skill is a different key");
		cache.Set("stats", "x", "   ");
		cache.TryGet("stats", "x", out _).Should().BeFalse("an empty/whitespace answer is never cached");
	}

	// ── Active-generation guard — idempotent end + many conversations ──────────
	[Fact]
	public void Active_guard_end_is_idempotent_and_per_conversation()
	{
		var guard = new OperatorAiActiveGenerationGuard();
		guard.End(99); // ending a conversation that never began must not throw
		guard.TryBegin(1).Should().BeTrue();
		guard.TryBegin(2).Should().BeTrue();
		guard.TryBegin(1).Should().BeFalse();
		guard.End(1);
		guard.End(1); // double end is safe
		guard.TryBegin(1).Should().BeTrue();
	}

	// ── Router WarmAsync ───────────────────────────────────────────────────────
	[Fact]
	public async Task Router_warm_returns_true_when_embeddings_available_false_when_not()
	{
		var skills = new IOperatorAiSkill[]
		{
			new MiniSkill("stats", "platform statistics counts"),
			new MiniSkill(OperatorAiSkillRegistry.GeneralAssistantId, "general help"),
		};
		using var mc = new MemoryCache(new MemoryCacheOptions());

		var okAi = new Mock<IAiGrpcService>();
		okAi.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(new float[] { 1, 0, 0, 0 }, "m", null));
		(await Router(skills, okAi, mc).WarmAsync()).Should().BeTrue();

		using var mc2 = new MemoryCache(new MemoryCacheOptions());
		var downAi = new Mock<IAiGrpcService>();
		downAi.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(null, null, "embed unavailable"));
		(await Router(skills, downAi, mc2).WarmAsync()).Should().BeFalse();
	}

	// ── AI-availability guard — streaming when disabled ────────────────────────
	[Fact]
	public async Task Availability_guard_streaming_yields_disabled_chunk_without_calling_inner()
	{
		var settings = new Mock<IOperatorAiSystemSettingsProvider>();
		settings.Setup(s => s.IsAiEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
		var inner = new AiGrpcService(
			Options.Create(new AiServiceOptions()),
			new ConfigurationBuilder().Build(),
			NullLogger<AiGrpcService>.Instance);
		var guard = new AiAvailabilityGuardGrpcService(inner, settings.Object);

		var chunks = new List<AiGenerateDelta>();
		await foreach (var c in guard.GenerateStreamAsync("hi"))
			chunks.Add(c);

		chunks.Should().ContainSingle();
		chunks[0].IsFinal.Should().BeTrue();
		chunks[0].ErrorCode.Should().Be("ai_disabled");
	}

	// ── StatsSkill non-streaming — terminal-plan branches ──────────────────────
	[Fact]
	public async Task Stats_nonstreaming_complete_plan_returns_answer_zero_extra_generations()
	{
		var retriever = Retriever(new[] { 0 });
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();
		orch.Setup(o => o.PrepareSelectedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiTerminalPlan("Users: 5 total.", null, 0, new OperatorAiLiveTurnTrace("count", 0, 1, 0, 1)));
		var ai = new Mock<IAiGrpcService>();

		var result = await new StatsSkill(retriever.Object, orch.Object, ai.Object).RunAsync(ReqS("how many users"), default);

		result.AnswerMarkdown.Should().Be("Users: 5 total.");
		result.Trace!.FastPath.Should().Be("count");
		result.Trace.Generations.Should().Be(0);
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Stats_nonstreaming_stream_prompt_generates_once_and_counts_generation()
	{
		var retriever = Retriever(new[] { 0 });
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();
		orch.Setup(o => o.PrepareSelectedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiTerminalPlan(null, "PROMPT", 512, new OperatorAiLiveTurnTrace("single-bundle", 0, 1, 0, 1)));
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("Albums: 42 total.");

		var result = await new StatsSkill(retriever.Object, orch.Object, ai.Object).RunAsync(ReqS("tell me about albums"), default);

		result.AnswerMarkdown.Should().Be("Albums: 42 total.");
		result.Trace!.Generations.Should().Be(1, "the single-bundle terminal generation counts as one");
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once());
	}

	[Fact]
	public async Task Stats_nonstreaming_generation_error_text_falls_back_to_plan_text()
	{
		var retriever = Retriever(new[] { 0 });
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();
		orch.Setup(o => o.PrepareSelectedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiTerminalPlan(null, "PROMPT", 512, new OperatorAiLiveTurnTrace("synthesis", 2, 1, 1, 2), FallbackAnswer: "Deterministic facts."));
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("Error: AI service unavailable (Unavailable)");

		var result = await new StatsSkill(retriever.Object, orch.Object, ai.Object).RunAsync(ReqS("compare"), default);

		result.AnswerMarkdown.Should().Be("Deterministic facts.", "a transport error string degrades to the stitched fallback, not the error");
	}

	// ── General-assistant + Moderation streaming (O4) ──────────────────────────
	[Fact]
	public async Task General_assistant_streams_deltas_then_final()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateStreamAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(StreamOf(new[] { "Hi ", "there." }, error: false));
		var skill = new GeneralAssistantSkill(ai.Object, Options.Create(new OperatorAiOptions { MaxNewTokens = 256 }));

		var chunks = await Collect(skill.RunStreamingAsync(ReqS("hello"), default));
		chunks.Where(c => !c.IsFinal).Select(c => c.Delta).Should().ContainInOrder("Hi ", "there.");
		chunks.Last().FinalAnswer.Should().Be("Hi there.");
	}

	[Fact]
	public async Task General_assistant_streaming_error_falls_back_to_default_reply()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateStreamAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(StreamOf(Array.Empty<string>(), error: true));
		var skill = new GeneralAssistantSkill(ai.Object, Options.Create(new OperatorAiOptions { MaxNewTokens = 256 }));

		var chunks = await Collect(skill.RunStreamingAsync(ReqS("hello"), default));
		chunks.Last().FinalAnswer.Should().Contain("platform statistics");
	}

	[Fact]
	public async Task Moderation_streams_from_aggregates_and_falls_back_on_error()
	{
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ModSnap(pending: 11));

		var okAi = new Mock<IAiGrpcService>();
		okAi.Setup(a => a.GenerateStreamAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(StreamOf(new[] { "11 pending." }, error: false));
		var okChunks = await Collect(new ModerationSkill(metrics.Object, okAi.Object, Options.Create(new OperatorAiOptions { MaxNewTokens = 256 }))
			.RunStreamingAsync(ReqS("backlog?"), default));
		okChunks.Last().FinalAnswer.Should().Be("11 pending.");

		var errAi = new Mock<IAiGrpcService>();
		errAi.Setup(a => a.GenerateStreamAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.Returns(StreamOf(Array.Empty<string>(), error: true));
		var errChunks = await Collect(new ModerationSkill(metrics.Object, errAi.Object, Options.Create(new OperatorAiOptions { MaxNewTokens = 256 }))
			.RunStreamingAsync(ReqS("backlog?"), default));
		errChunks.Last().FinalAnswer.Should().Contain("11 pending", "the deterministic aggregate fallback never leaks raw content");
	}

	// ── helpers ────────────────────────────────────────────────────────────────
	private static OperatorAiSkillRequest ReqS(string m) => new(m, 1, Array.Empty<ChatHistoryEntry>(), 1, 1.0);

	private static async Task<List<OperatorAiStreamChunk>> Collect(IAsyncEnumerable<OperatorAiStreamChunk> stream)
	{
		var list = new List<OperatorAiStreamChunk>();
		await foreach (var c in stream)
			list.Add(c);
		return list;
	}

	private static async IAsyncEnumerable<AiGenerateDelta> StreamOf(string[] deltas, bool error)
	{
		foreach (var d in deltas)
		{
			await Task.Yield();
			yield return new AiGenerateDelta(d, false, null, null, null);
		}
		yield return error
			? new AiGenerateDelta(null, true, null, "ollama offline", "stream_failed")
			: new AiGenerateDelta(string.Empty, true, "stop", null, null);
	}

	private static ContentModerationMetricsSnapshot ModSnap(int pending) =>
		new(pending, 1, 0, 0, DateTime.UtcNow, 9, 1.0, 2.0, 10, 3, 1, 8, 2, 4, 0,
			Array.Empty<FlagCountDto>(), Array.Empty<FacePendingCountDto>());

	private static Mock<IOperatorAiRetriever> Retriever(int[] indices)
	{
		var r = new Mock<IOperatorAiRetriever>();
		r.Setup(x => x.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(indices, OperatorAiSelectionStrategy.Rag, Array.Empty<OperatorAiRetrievalHit>(), false, false, 1, 1));
		return r;
	}

	private static OperatorAiBundleCacheEntry Ready(int i, string json) =>
		new(i, OperatorAiEntityBundleCatalog.GetByIndex(i).Id, OperatorAiBundleCacheState.Ready, json, null, DateTime.UtcNow, DateTime.UtcNow);

	private static OperatorAiBundleCacheEntry Failed(int i) =>
		new(i, OperatorAiEntityBundleCatalog.GetByIndex(i).Id, OperatorAiBundleCacheState.Failed, null, "boom", DateTime.UtcNow, DateTime.UtcNow);

	private static Mock<IOperatorAiLiveStatsPrefetcher> Prefetch(int[] indices, string json)
	{
		var p = new Mock<IOperatorAiLiveStatsPrefetcher>();
		p.Setup(x => x.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(idx.ToDictionary(i => i, i => Ready(i, json)), idx.Count, 0, idx.Count, 0));
		return p;
	}

	private static OperatorAiLiveStatsOrchestrator BuildOrch(
		Mock<IAiGrpcService> ai,
		Mock<IOperatorAiLiveStatsPrefetcher> prefetch,
		bool synthesis = false,
		bool isCount = false,
		string? helperModel = null,
		bool parallelMap = false)
	{
		var decisions = new Mock<IOperatorAiDecisionHelper>();
		decisions.Setup(d => d.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isCount);
		var options = Options.Create(new OperatorAiOptions
		{
			MaxParallelBundleAiCalls = 2,
			LiveUseAiSynthesisStitch = synthesis,
			LiveBundleMaxNewTokens = 96,
			OverallTurnBudgetMs = 10000,
			PerBundleGenerateTimeoutMs = 5000,
			HelperParallelMapEnabled = parallelMap,
		});
		return new OperatorAiLiveStatsOrchestrator(
			prefetch.Object, ai.Object, decisions.Object, options,
			Options.Create(new AiServiceOptions { HelperModel = helperModel }),
			NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
	}

	private static OperatorAiSkillRouter Router(IOperatorAiSkill[] skills, Mock<IAiGrpcService> ai, IMemoryCache mc) =>
		new(new OperatorAiSkillRegistry(skills), new OperatorAiSkillVectorCache(), ai.Object, mc,
			Options.Create(new AiServiceOptions { EmbeddingModel = "m", EmbeddingDim = 4 }),
			Options.Create(new OperatorAiOptions { SkillRoutingMinScore = 0.1, EmbedTimeoutMs = 2000 }),
			NullLogger<OperatorAiSkillRouter>.Instance);

	private sealed class MiniSkill : IOperatorAiSkill
	{
		private readonly string _desc;
		public MiniSkill(string id, string desc) { Id = id; _desc = desc; }
		public string Id { get; }
		public string DisplayName => Id;
		public string Description => _desc;
		public IReadOnlyList<string> SampleRequests => Array.Empty<string>();
		public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;
		public Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken) =>
			Task.FromResult(new OperatorAiSkillResult($"ran:{Id}"));
	}
}
