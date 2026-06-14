using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi.Skills;

/// <summary>
/// Edge cases for the four v1 skills (SK-4…SK-7): the stats skill (RAG-v1 lift incl. zero-hit refusal), the reports
/// skill (type detection + deterministic GenerateReport), the moderation skill (aggregate-only, no raw content), and
/// the general-assistant fallback (no retrieval, no fabricated numbers).
/// </summary>
public sealed class OperatorAiSkillsTests
{
	private static OperatorAiSkillRequest Req(string message, int? parallel = 2) =>
		new(message, 1, Array.Empty<ChatHistoryEntry>(), parallel, 1.0);

	private static OperatorAiOptions Opts() => new() { MaxNewTokens = 256, LiveStitchMaxNewTokens = 512 };

	/// <summary>Decision helper stub: helper disabled ⇒ report-type detection uses the deterministic heuristic (7B-perf O19 fallback).</summary>
	private static IOperatorAiDecisionHelper Decisions()
	{
		var d = new Mock<IOperatorAiDecisionHelper>();
		d.Setup(x => x.DetectReportTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string m, CancellationToken _) => OperatorAiReportTypeHeuristic.Detect(m));
		d.Setup(x => x.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string m, CancellationToken _) => OperatorAiStatsIntent.IsSimpleCountQuestion(m));
		d.Setup(x => x.IsBroadOverviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string m, CancellationToken _) => OperatorAiStatsIntent.IsBroadOverviewQuestion(m));
		return d.Object;
	}

	// ── StatsSkill ──────────────────────────────────────────────────────────

	[Fact]
	public async Task Stats_zero_hit_returns_fixed_refusal_and_does_not_call_orchestrator()
	{
		var retriever = new Mock<IOperatorAiRetriever>();
		retriever.Setup(r => r.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(
				Array.Empty<int>(), OperatorAiSelectionStrategy.ZeroHit, Array.Empty<OperatorAiRetrievalHit>(), false, false, 0, 0));
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();

		var result = await new StatsSkill(retriever.Object, orch.Object, Mock.Of<IAiGrpcService>(), Decisions()).RunAsync(Req("what is the weather"), default);

		result.AnswerMarkdown.Should().Be(StatsSkill.ZeroHitRefusal);
		orch.Verify(o => o.PrepareSelectedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Stats_happy_path_runs_map_stitch_over_selected_bundles()
	{
		var retriever = new Mock<IOperatorAiRetriever>();
		retriever.Setup(r => r.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(
				new[] { 0, 12 }, OperatorAiSelectionStrategy.Rag, Array.Empty<OperatorAiRetrievalHit>(), false, false, 5, 7));
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();
		// The skill now drives the terminal plan; a ready CompleteAnswer (count fast-path style) is returned as-is.
		orch.Setup(o => o.PrepareSelectedAsync("how many users?", It.Is<IReadOnlyList<int>>(i => i.SequenceEqual(new[] { 0, 12 })), 2, false, false, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiTerminalPlan("There are 1,234 users.", null, 0, new OperatorAiLiveTurnTrace("count", 0, 0, 0, 2)));

		var result = await new StatsSkill(retriever.Object, orch.Object, Mock.Of<IAiGrpcService>(), Decisions()).RunAsync(Req("how many users?"), default);

		result.AnswerMarkdown.Should().Be("There are 1,234 users.");
		result.Trace!.SkillId.Should().Be("stats");
	}

	[Fact]
	public async Task Stats_broad_overview_maps_all_61_bundles_rag_ranked_first_and_sets_broad_flag()
	{
		// Retriever returns only the top-K (here 2), RAG-ranked: {12, 0}.
		var retriever = new Mock<IOperatorAiRetriever>();
		retriever.Setup(r => r.RetrieveBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiRetrievalResult(
				new[] { 12, 0 }, OperatorAiSelectionStrategy.Rag, Array.Empty<OperatorAiRetrievalHit>(), false, false, 5, 7));

		IReadOnlyList<int>? captured = null;
		var orch = new Mock<IOperatorAiLiveStatsOrchestrator>();
		// Broad call: appendCoverageNote AND broadOverview are both true.
		orch.Setup(o => o.PrepareSelectedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), 2, true, true, It.IsAny<CancellationToken>()))
			.Callback((string _, IReadOnlyList<int> idx, int _, bool _, bool _, CancellationToken _) => captured = idx)
			.ReturnsAsync(new OperatorAiTerminalPlan("Full platform snapshot.", null, 0, new OperatorAiLiveTurnTrace("stitch", 0, 0, 0, OperatorAiEntityBundleCatalog.BundleCount)));

		await new StatsSkill(retriever.Object, orch.Object, Mock.Of<IAiGrpcService>(), Decisions()).RunAsync(Req("give me full statistics"), default);

		captured.Should().NotBeNull("broad-overview must reach the orchestrator (no zero-hit refusal)");
		captured!.Should().HaveCount(OperatorAiEntityBundleCatalog.BundleCount, "broad-overview maps ALL 61 bundles");
		captured.Should().OnlyHaveUniqueItems();
		captured.Should().BeEquivalentTo(Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount), "every bundle index is present exactly once");
		captured!.Take(2).Should().Equal(new[] { 12, 0 }, "the RAG-ranked indices lead, then the rest follow in catalog order");
	}

	// ── ReportsSkill ────────────────────────────────────────────────────────

	[Theory]
	[InlineData("generate a moderation backlog report", "moderation_backlog")]
	[InlineData("show me the moderation queue report", "moderation_backlog")]
	[InlineData("produce a face health report", "face_health")]
	[InlineData("grid completeness report", "grid_completeness")]
	[InlineData("report on the component types", "grid_completeness")]
	[InlineData("hello there", null)]
	[InlineData("how many users", null)]
	public void Reports_detects_type(string message, string? expected)
	{
		ReportsSkill.DetectReportType(message).Should().Be(expected);
	}

	[Fact]
	public async Task Reports_moderation_backlog_assembles_input_and_returns_markdown()
	{
		var ai = new Mock<IAiGrpcService>();
		string? capturedType = null, capturedJson = null;
		ai.Setup(a => a.GenerateReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string j, int _, CancellationToken __) => { capturedType = t; capturedJson = j; return new AiGenerateReportResult("# Moderation backlog\n\n5 pending.", "{}", "report-v1", null); });
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snap(pending: 5, oldest: 9));

		var skill = new ReportsSkill(ai.Object, metrics.Object, Mock.Of<IDbContextFactory<ApplicationDbContext>>(), Decisions(), Options.Create(Opts()));
		var result = await skill.RunAsync(Req("generate a moderation backlog report"), default);

		capturedType.Should().Be("moderation_backlog");
		capturedJson.Should().Contain("pendingCount").And.Contain("5");
		result.AnswerMarkdown.Should().Contain("Moderation backlog");
	}

	[Fact]
	public async Task Reports_ambiguous_request_offers_the_choices()
	{
		var skill = new ReportsSkill(Mock.Of<IAiGrpcService>(), Mock.Of<IContentModerationMetrics>(), Mock.Of<IDbContextFactory<ApplicationDbContext>>(), Decisions(), Options.Create(Opts()));
		var result = await skill.RunAsync(Req("make me a report"), default);
		result.AnswerMarkdown.Should().Contain("face health").And.Contain("moderation backlog").And.Contain("grid completeness");
	}

	[Fact]
	public async Task Reports_worker_error_is_graceful()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiGenerateReportResult(null, null, null, "ai_disabled"));
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snap());

		var skill = new ReportsSkill(ai.Object, metrics.Object, Mock.Of<IDbContextFactory<ApplicationDbContext>>(), Decisions(), Options.Create(Opts()));
		var result = await skill.RunAsync(Req("moderation backlog report"), default);
		result.AnswerMarkdown.Should().Contain("could not be generated");
	}

	// ── ModerationSkill ─────────────────────────────────────────────────────

	[Fact]
	public async Task Moderation_answers_from_aggregate_metrics_only()
	{
		var ai = new Mock<IAiGrpcService>();
		string? prompt = null;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string p, int _, string? __, string? ___, double? _t, IReadOnlyList<string>? _s, string? _m, CancellationToken ____) => { prompt = p; return "There are 5 pending submissions."; });
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snap(pending: 5));

		var result = await new ModerationSkill(metrics.Object, ai.Object, Options.Create(Opts())).RunAsync(Req("how big is the backlog?"), default);

		result.AnswerMarkdown.Should().Contain("5 pending");
		prompt.Should().NotBeNull();
		prompt!.Should().Contain("aggregate counts").And.Contain("pendingSubmissions");
		new ModerationSkill(metrics.Object, ai.Object, Options.Create(Opts())).Trust.Should().Be(OperatorAiSkillTrust.Trusted);
	}

	// ── GeneralAssistantSkill ───────────────────────────────────────────────

	[Fact]
	public async Task General_assistant_replies_without_retrieval()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("Hi! I can help with platform statistics, reports, and the moderation backlog.");

		var skill = new GeneralAssistantSkill(ai.Object, Options.Create(Opts()));
		skill.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
		var result = await skill.RunAsync(Req("hello"), default);
		result.AnswerMarkdown.Should().Contain("help");
		result.Trace!.UsedRetrieval.Should().BeFalse();
	}

	[Fact]
	public async Task General_assistant_has_a_safe_fallback_when_model_returns_empty()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);
		var result = await new GeneralAssistantSkill(ai.Object, Options.Create(Opts())).RunAsync(Req("hmm"), default);
		result.AnswerMarkdown.Should().NotBeNullOrWhiteSpace();
	}

	private static ContentModerationMetricsSnapshot Snap(int pending = 5, double? oldest = 12) =>
		new(
			PendingSubmissions: pending,
			AiQueuedJobs: 1,
			AiProcessingJobs: 0,
			AiFailedJobs: 0,
			OldestPendingSubmissionUtc: DateTime.UtcNow,
			OldestPendingAgeHours: oldest,
			AverageReviewLatencyHours: 1.0,
			P95ReviewLatencyHours: 2.0,
			ApprovedCount: 10,
			RejectedCount: 3,
			RemovedCount: 1,
			RecommendedApproveCount: 8,
			RecommendedRejectCount: 2,
			NeedsHumanReviewCount: 4,
			AiJobsLikelyTimeoutCount: 0,
			TopModerationFlags: Array.Empty<FlagCountDto>(),
			PendingSubmissionsByFace: Array.Empty<FacePendingCountDto>());
}
