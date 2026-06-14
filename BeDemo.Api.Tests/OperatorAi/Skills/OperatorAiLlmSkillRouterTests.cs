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

namespace BeDemo.Api.Tests.OperatorAi.Skills;

/// <summary>
/// Edge cases for the LLM skill router (operator-ai LLM skill router): the new decision ORDER in
/// <see cref="OperatorAiSkillRouter.RouteAsync"/> — (1) deterministic broad-overview keyword pre-route → stats,
/// (2) 3B helper single-label classification (runs BEFORE the embed early-returns, D.1), (3) cosine fallback — plus
/// the two new <see cref="IOperatorAiDecisionHelper"/> primitives (<c>DetectSkillAsync</c> label→id parsing and the
/// gated <c>IsBroadOverviewAsync</c> upgrade). The headline regression this fixes: "Give me full entities statistics"
/// must reach the stats skill, and "moderation queue count" must NOT be force-routed to the reports skill.
/// </summary>
public sealed class OperatorAiLlmSkillRouterTests
{
	// ── Fakes ────────────────────────────────────────────────────────────────
	private sealed class FakeSkill : IOperatorAiSkill
	{
		private readonly string _marker;
		public FakeSkill(string id, string marker) { Id = id; _marker = marker; }
		public string Id { get; }
		public string DisplayName => Id;
		public string Description => _marker;
		public IReadOnlyList<string> SampleRequests => Array.Empty<string>();
		public string RouterHint => _marker;
		public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;
		public Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken) =>
			Task.FromResult(new OperatorAiSkillResult($"ran:{Id}"));
	}

	// Marker → one-hot vector (stats=M0, reports=M1, moderation=M2, general=Mgen). No marker ⇒ diffuse (cosine 0.5).
	private static float[] EmbedFor(string text) =>
		text.Contains("M0") ? [1, 0, 0, 0] :
		text.Contains("M1") ? [0, 1, 0, 0] :
		text.Contains("M2") ? [0, 0, 1, 0] :
		text.Contains("Mgen") ? [0, 0, 0, 1] : [1, 1, 1, 1];

	private static (OperatorAiSkillRouter Router, Mock<IOperatorAiDecisionHelper> Helper, Mock<IAiGrpcService> Ai) Build(
		AiEmbedTextResult? forceEmbed = null, double threshold = 0.9)
	{
		var skills = new IOperatorAiSkill[]
		{
			new FakeSkill("stats", "M0"),
			new FakeSkill("reports", "M1"),
			new FakeSkill("moderation", "M2"),
			new FakeSkill(OperatorAiSkillRegistry.GeneralAssistantId, "Mgen"),
		};
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string? _, CancellationToken __) => forceEmbed ?? new AiEmbedTextResult(EmbedFor(t), "model-a", null));

		// Default helper: abstains (DetectSkill → null, IsBroadOverview → deterministic keyword), so each test opts in.
		var helper = new Mock<IOperatorAiDecisionHelper>();
		helper.Setup(h => h.DetectSkillAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, string)>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string?)null);
		helper.Setup(h => h.IsBroadOverviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string m, CancellationToken _) => OperatorAiStatsIntent.IsBroadOverviewQuestion(m));

		var router = new OperatorAiSkillRouter(
			new OperatorAiSkillRegistry(skills), new OperatorAiSkillVectorCache(), helper.Object, ai.Object,
			new MemoryCache(new MemoryCacheOptions()),
			Options.Create(new AiServiceOptions { EmbeddingModel = "model-a", EmbeddingDim = 4 }),
			Options.Create(new OperatorAiOptions { SkillRoutingMinScore = threshold, EmbedTimeoutMs = 2000 }),
			NullLogger<OperatorAiSkillRouter>.Instance);
		return (router, helper, ai);
	}

	private static void HelperPicks(Mock<IOperatorAiDecisionHelper> helper, string? id) =>
		helper.Setup(h => h.DetectSkillAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, string)>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(id);

	// ── (1) Broad-overview keyword pre-route → stats ──────────────────────────

	[Fact]
	public async Task Broad_keyword_pre_routes_to_stats_before_consulting_the_helper()
	{
		var (router, helper, _) = Build();
		HelperPicks(helper, "reports"); // even an over-eager helper must not win over the broad pre-route

		var route = await router.RouteAsync("Give me full entities statistics");

		route.Skill.Id.Should().Be("stats", "a whole-platform overview is unambiguously the stats skill");
		route.Fallback.Should().BeFalse();
		route.Score.Should().Be(1.0, "a deterministic pre-route is stamped with the non-cosine sentinel score");
		helper.Verify(h => h.DetectSkillAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, string)>>(), It.IsAny<CancellationToken>()), Times.Never(),
			"the broad pre-route short-circuits before the 3B helper");
	}

	// ── (2) 3B helper precedence + helper-before-embed (D.1) ──────────────────

	[Fact]
	public async Task Helper_decision_beats_cosine()
	{
		var (router, helper, _) = Build();
		HelperPicks(helper, "reports");

		// Cosine alone would pick stats (M0); the helper overrides to reports.
		var route = await router.RouteAsync("M0 please produce something");

		route.Skill.Id.Should().Be("reports");
		route.Fallback.Should().BeFalse();
		route.Score.Should().Be(1.0);
	}

	[Fact]
	public async Task Helper_routes_even_when_embeddings_are_unavailable()
	{
		// D.1: the helper runs BEFORE the embed early-return, so it still routes when the embedding worker is down.
		var (router, helper, ai) = Build(forceEmbed: new AiEmbedTextResult(null, null, "ollama down"));
		HelperPicks(helper, "moderation");

		var route = await router.RouteAsync("a message with no marker at all");

		route.Skill.Id.Should().Be("moderation", "the helper decides without needing embeddings");
		route.Fallback.Should().BeFalse();
		ai.Verify(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never(),
			"a confident helper answer means we never reach the embedding step");
	}

	[Fact]
	public async Task Helper_general_label_routes_to_general_assistant_with_fallback_flag()
	{
		var (router, helper, _) = Build();
		HelperPicks(helper, OperatorAiSkillRegistry.GeneralAssistantId);

		var route = await router.RouteAsync("M0 stats-looking but the helper says general");

		route.Skill.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
		route.Fallback.Should().BeTrue("routing to the general-assistant is always reported as a fallback");
	}

	// ── (3) Cosine fallback when the helper abstains / is wrong ────────────────

	[Fact]
	public async Task Helper_null_falls_through_to_cosine()
	{
		var (router, _, _) = Build();
		var route = await router.RouteAsync("M1 a clear reports match"); // helper abstains by default
		route.Skill.Id.Should().Be("reports");
		route.Fallback.Should().BeFalse();
		route.Score.Should().BeGreaterThanOrEqualTo(0.9, "the cosine path stamps the real similarity, not the sentinel");
	}

	[Fact]
	public async Task Helper_unknown_id_falls_through_to_cosine()
	{
		var (router, helper, _) = Build();
		HelperPicks(helper, "does-not-exist"); // unparseable / unknown id ⇒ cosine, never a crash

		var route = await router.RouteAsync("M2 a clear moderation match");

		route.Skill.Id.Should().Be("moderation");
		route.Fallback.Should().BeFalse();
	}

	// ── Regression: moderation-queue count must NOT be forced to the reports skill ──

	[Fact]
	public async Task Moderation_queue_count_is_not_pre_routed_to_reports()
	{
		// The deterministic report-type heuristic maps "moderation … queue" to a report; the router must NOT pre-route
		// on it (that stole the count). With the report pre-route dropped, the helper decides — here: stats.
		var (router, helper, _) = Build();
		HelperPicks(helper, "stats");

		var route = await router.RouteAsync("how many items are in the moderation queue?");

		route.Skill.Id.Should().Be("stats", "a count question is the stats skill even when it mentions the moderation queue");
		route.Skill.Id.Should().NotBe("reports");
	}

	[Fact]
	public async Task RouterLabel_maps_general_assistant_to_a_single_token()
	{
		OperatorAiSkillRouter.RouterLabel(OperatorAiSkillRegistry.GeneralAssistantId).Should().Be("general");
		OperatorAiSkillRouter.RouterLabel("stats").Should().Be("stats");
		OperatorAiSkillRouter.RouterLabel("moderation").Should().Be("moderation");
	}

	// ── DetectSkillAsync (helper primitive) ───────────────────────────────────
	private static readonly IReadOnlyList<(string Id, string Label, string Hint)> Candidates =
	[
		("stats", "stats", "counts and totals"),
		("reports", "reports", "generate a report document"),
		("moderation", "moderation", "the moderation backlog"),
		(OperatorAiSkillRegistry.GeneralAssistantId, "general", "greetings and help"),
	];

	private static OperatorAiDecisionHelper Helper(Mock<IAiGrpcService> ai, bool enabled = true, string? model = "qwen2.5:3b") =>
		new(ai.Object,
			Options.Create(new AiServiceOptions { HelperModel = model }),
			Options.Create(new OperatorAiOptions { HelperForDecisions = enabled }),
			NullLogger<OperatorAiDecisionHelper>.Instance);

	private static void Generates(Mock<IAiGrpcService> ai, string text) =>
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(text);

	[Fact]
	public async Task DetectSkill_disabled_returns_null_without_calling_the_model()
	{
		var ai = new Mock<IAiGrpcService>();
		(await Helper(ai, enabled: false).DetectSkillAsync("anything", Candidates)).Should().BeNull();
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task DetectSkill_empty_candidates_returns_null()
	{
		var ai = new Mock<IAiGrpcService>();
		(await Helper(ai).DetectSkillAsync("anything", Array.Empty<(string, string, string)>())).Should().BeNull();
	}

	[Theory]
	[InlineData("reports", "reports")]
	[InlineData("Reports.", "reports")] // capitalisation + trailing punctuation tolerated
	[InlineData("moderation", "moderation")]
	[InlineData("general", "general-assistant")] // single-token label maps back to the hyphenated registry id
	public async Task DetectSkill_parses_label_back_to_registry_id(string reply, string expectedId)
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, reply);
		(await Helper(ai).DetectSkillAsync("some message", Candidates)).Should().Be(expectedId);
	}

	[Fact]
	public async Task DetectSkill_unparseable_reply_returns_null()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, "none of these"); // no candidate label present ⇒ defer to cosine
		(await Helper(ai).DetectSkillAsync("some message", Candidates)).Should().BeNull();
	}

	[Fact]
	public async Task DetectSkill_exception_returns_null()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("worker down"));
		(await Helper(ai).DetectSkillAsync("some message", Candidates)).Should().BeNull();
	}

	[Fact]
	public async Task DetectSkill_uses_the_helper_model_and_deterministic_sampling()
	{
		var ai = new Mock<IAiGrpcService>();
		string? usedModel = null; double? usedTemp = null;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int __, string? ___, string? ____, double? temp, IReadOnlyList<string>? _____, string? model, CancellationToken _6) =>
				{ usedModel = model; usedTemp = temp; return "stats"; });

		await Helper(ai, model: "qwen2.5:3b").DetectSkillAsync("how many users", Candidates);

		usedModel.Should().Be("qwen2.5:3b", "routing runs on the CPU helper model");
		usedTemp.Should().Be(0.0, "classification is deterministic");
	}

	// ── IsBroadOverviewAsync (gated helper upgrade) ───────────────────────────

	[Fact]
	public async Task IsBroadOverview_keyword_hit_is_true_without_the_model()
	{
		var ai = new Mock<IAiGrpcService>();
		(await Helper(ai).IsBroadOverviewAsync("give me full statistics")).Should().BeTrue();
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never(),
			"a deterministic keyword hit never needs the 3B");
	}

	[Fact]
	public async Task IsBroadOverview_disabled_helper_uses_deterministic_only()
	{
		var ai = new Mock<IAiGrpcService>();
		(await Helper(ai, enabled: false).IsBroadOverviewAsync("statistics trends")).Should().BeFalse();
	}

	[Fact]
	public async Task IsBroadOverview_simple_count_is_never_upgraded()
	{
		// "how many users" is a simple count (never broad) ⇒ short-circuit, the 3B is not consulted.
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, "YES"); // even if the model would say yes, the gate skips it
		OperatorAiStatsIntent.IsSimpleCountQuestion("how many users").Should().BeTrue();

		(await Helper(ai).IsBroadOverviewAsync("how many users")).Should().BeFalse();
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task IsBroadOverview_keyword_miss_metrics_like_is_upgraded_when_the_model_says_yes()
	{
		const string msg = "statistics trends"; // metrics-like, not broad-by-keyword, not a simple count
		OperatorAiStatsIntent.IsBroadOverviewQuestion(msg).Should().BeFalse();
		OperatorAiStatsIntent.IsMetricsQuestion(msg).Should().BeTrue();
		OperatorAiStatsIntent.IsSimpleCountQuestion(msg).Should().BeFalse();

		var ai = new Mock<IAiGrpcService>();
		Generates(ai, "YES");
		(await Helper(ai).IsBroadOverviewAsync(msg)).Should().BeTrue("the 3B upgrades a keyword-miss to broad");
	}

	[Fact]
	public async Task IsBroadOverview_model_no_keeps_deterministic_false()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, "NO");
		(await Helper(ai).IsBroadOverviewAsync("statistics trends")).Should().BeFalse();
	}
}
