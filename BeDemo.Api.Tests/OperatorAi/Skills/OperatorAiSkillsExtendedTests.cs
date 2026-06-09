using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi.Skills;

/// <summary>
/// Extended edge cases for the skills v1 slice: the EF-backed report branches (face_health / grid_completeness),
/// registry resolution, router threshold/dimension edges, moderation empty-generate + no-raw-content guarantees,
/// general-assistant prompt safety + token cap, and the per-skill trust declarations.
/// </summary>
public sealed class OperatorAiSkillsExtendedTests
{
	private static OperatorAiSkillRequest Req(string message) =>
		new(message, 1, Array.Empty<ChatHistoryEntry>(), 2, 1.0);

	private static OperatorAiOptions Opts(int maxNewTokens = 2048) =>
		new() { MaxNewTokens = maxNewTokens, LiveStitchMaxNewTokens = 512 };

	private sealed class FakeSkill : IOperatorAiSkill
	{
		private readonly string _marker;
		public FakeSkill(string id, string marker = "") { Id = id; _marker = marker; }
		public string Id { get; }
		public string DisplayName => Id;
		public string Description => _marker;
		public IReadOnlyList<string> SampleRequests => Array.Empty<string>();
		public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;
		public Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken) =>
			Task.FromResult(new OperatorAiSkillResult($"ran:{Id}"));
	}

	private static IDbContextFactory<ApplicationDbContext> NewFactory(out ServiceProvider sp)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContextFactory<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
		sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
	}

	private static ContentModerationMetricsSnapshot Snap(int pending = 5, IReadOnlyList<FlagCountDto>? flags = null) =>
		new(pending, 1, 0, 0, DateTime.UtcNow, 9, 1.0, 2.0, 10, 3, 1, 8, 2, 4, 0,
			flags ?? Array.Empty<FlagCountDto>(), Array.Empty<FacePendingCountDto>());

	// ── ReportsSkill EF branches ─────────────────────────────────────────────

	[Fact]
	public async Task Reports_face_health_counts_faces()
	{
		var factory = NewFactory(out var sp);
		await using (var db = factory.CreateDbContext())
		{
			db.Faces.Add(new Face { Index = "a", Title = "A", IsPublic = true, CreatedAt = DateTime.UtcNow });
			db.Faces.Add(new Face { Index = "b", Title = "B", IsPublic = true, CreatedAt = DateTime.UtcNow });
			await db.SaveChangesAsync();
		}

		var ai = new Mock<IAiGrpcService>();
		string? type = null, json = null;
		ai.Setup(a => a.GenerateReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string j, int _, CancellationToken __) => { type = t; json = j; return new AiGenerateReportResult("# Face health", "{}", "report-v1", null); });

		var skill = new ReportsSkill(ai.Object, Mock.Of<IContentModerationMetrics>(), factory, Options.Create(Opts()));
		await skill.RunAsync(Req("produce a face health report"), default);

		type.Should().Be("face_health");
		json.Should().Contain("All faces (2)").And.Contain("pageCount");
		await sp.DisposeAsync();
	}

	[Fact]
	public async Task Reports_grid_completeness_counts_component_types()
	{
		// ComponentTypes carries seeded reference rows; the skill counts them (no manual seed needed — and InMemory
		// would conflict on the non-generated Id key). This exercises the CountAsync branch + input_json assembly.
		var factory = NewFactory(out var sp);

		var ai = new Mock<IAiGrpcService>();
		string? type = null, json = null;
		ai.Setup(a => a.GenerateReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string j, int _, CancellationToken __) => { type = t; json = j; return new AiGenerateReportResult("# Grid", "{}", "report-v1", null); });

		var skill = new ReportsSkill(ai.Object, Mock.Of<IContentModerationMetrics>(), factory, Options.Create(Opts()));
		await skill.RunAsync(Req("grid completeness report"), default);

		type.Should().Be("grid_completeness");
		// ComponentTypes carries seeded reference rows, so the exact count is environment-dependent; assert the
		// deterministic input_json shape (the CountAsync branch ran and produced the right keys).
		json.Should().Contain("componentTypeCount").And.Contain("missingTypes");
		await sp.DisposeAsync();
	}

	[Theory]
	[InlineData("MODERATION BACKLOG please", "moderation_backlog")]
	[InlineData("a face report", "face_health")]
	[InlineData("a grid report", "grid_completeness")]
	[InlineData("report", null)]
	[InlineData("", null)]
	public void Reports_detect_type_extra_cases(string message, string? expected) =>
		ReportsSkill.DetectReportType(message).Should().Be(expected);

	// ── ModerationSkill ──────────────────────────────────────────────────────

	[Fact]
	public async Task Moderation_empty_generate_falls_back_deterministically()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snap(pending: 7));

		var result = await new ModerationSkill(metrics.Object, ai.Object, Options.Create(Opts())).RunAsync(Req("backlog?"), default);
		result.AnswerMarkdown.Should().Contain("7");
	}

	[Fact]
	public async Task Moderation_prompt_has_aggregates_top_flags_and_no_raw_content()
	{
		var ai = new Mock<IAiGrpcService>();
		string? prompt = null;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string p, int _, string? __, string? ___, CancellationToken ____) => { prompt = p; return "ok"; });
		var metrics = new Mock<IContentModerationMetrics>();
		metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(Snap(pending: 5, flags: new[] { new FlagCountDto("spam", 4) }));

		await new ModerationSkill(metrics.Object, ai.Object, Options.Create(Opts())).RunAsync(Req("top flags?"), default);

		prompt.Should().Contain("approvedCount").And.Contain("spam");
		prompt!.Should().NotContain("@", "the moderation skill must never put raw user content (e.g. emails) in the prompt");
	}

	// ── GeneralAssistantSkill ────────────────────────────────────────────────

	[Fact]
	public async Task General_assistant_prompt_forbids_fabrication_and_caps_tokens()
	{
		var ai = new Mock<IAiGrpcService>();
		string? prompt = null;
		var maxTokens = -1;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string p, int mt, string? _, string? __, CancellationToken ___) => { prompt = p; maxTokens = mt; return "hi"; });

		await new GeneralAssistantSkill(ai.Object, Options.Create(Opts(maxNewTokens: 2048))).RunAsync(Req("hello"), default);

		prompt.Should().Contain("NO platform data").And.Contain("do not state or invent");
		maxTokens.Should().BeLessThanOrEqualTo(256, "the fallback reply is capped short regardless of MaxNewTokens");
	}

	// ── Trust declarations ───────────────────────────────────────────────────

	[Fact]
	public void All_v1_skills_are_trusted()
	{
		new StatsSkill(Mock.Of<IOperatorAiRetriever>(), Mock.Of<IOperatorAiLiveStatsOrchestrator>()).Trust.Should().Be(OperatorAiSkillTrust.Trusted);
		new ReportsSkill(Mock.Of<IAiGrpcService>(), Mock.Of<IContentModerationMetrics>(), Mock.Of<IDbContextFactory<ApplicationDbContext>>(), Options.Create(Opts())).Trust.Should().Be(OperatorAiSkillTrust.Trusted);
		new ModerationSkill(Mock.Of<IContentModerationMetrics>(), Mock.Of<IAiGrpcService>(), Options.Create(Opts())).Trust.Should().Be(OperatorAiSkillTrust.Trusted);
		new GeneralAssistantSkill(Mock.Of<IAiGrpcService>(), Options.Create(Opts())).Trust.Should().Be(OperatorAiSkillTrust.Trusted);
	}

	// ── Registry ─────────────────────────────────────────────────────────────

	[Fact]
	public void Registry_resolves_by_id_case_insensitively_and_lists_all()
	{
		var skills = new IOperatorAiSkill[]
		{
			new FakeSkill("stats"), new FakeSkill("reports"), new FakeSkill(OperatorAiSkillRegistry.GeneralAssistantId),
		};
		var registry = new OperatorAiSkillRegistry(skills);

		registry.All.Should().HaveCount(3);
		registry.GetById("STATS")!.Id.Should().Be("stats");
		registry.GetById("nope").Should().BeNull();
		registry.GetById("").Should().BeNull();
		registry.GeneralAssistant.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
	}

	// ── Router edges ─────────────────────────────────────────────────────────

	private static (OperatorAiSkillRouter Router, Mock<IAiGrpcService> Ai) BuildRouter(double threshold)
	{
		var skills = new IOperatorAiSkill[]
		{
			new FakeSkill("stats", "M0"), new FakeSkill("reports", "M1"),
			new FakeSkill("moderation", "M2"), new FakeSkill(OperatorAiSkillRegistry.GeneralAssistantId, "Mgen"),
		};
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string? _, CancellationToken __) =>
			{
				float[] v = t.Contains("M0") ? [1, 0, 0, 0] : t.Contains("M1") ? [0, 1, 0, 0] :
					t.Contains("M2") ? [0, 0, 1, 0] : t.Contains("Mgen") ? [0, 0, 0, 1] : [1, 1, 1, 1];
				return new AiEmbedTextResult(v, "model-a", null);
			});
		var router = new OperatorAiSkillRouter(
			new OperatorAiSkillRegistry(skills), new OperatorAiSkillVectorCache(), ai.Object,
			Options.Create(new AiServiceOptions { EmbeddingModel = "model-a", EmbeddingDim = 4 }),
			Options.Create(new OperatorAiOptions { SkillRoutingMinScore = threshold, EmbedTimeoutMs = 2000 }),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<OperatorAiSkillRouter>.Instance);
		return (router, ai);
	}

	[Fact]
	public async Task Router_with_threshold_above_max_always_falls_back()
	{
		var (router, _) = BuildRouter(threshold: 1.1); // cosine can never reach 1.1
		var route = await router.RouteAsync("M0 a clear stats match");
		route.Skill.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
		route.Fallback.Should().BeTrue();
	}

	[Fact]
	public async Task Router_selects_when_score_meets_threshold_exactly()
	{
		var (router, _) = BuildRouter(threshold: 1.0); // exact match scores 1.0; `< threshold` is false at equality
		var route = await router.RouteAsync("M1 reports please");
		route.Skill.Id.Should().Be("reports");
		route.Fallback.Should().BeFalse();
	}

	[Fact]
	public void Dot_and_normalize_tolerate_dimension_mismatch()
	{
		var a = OperatorAiSkillRouter.Normalize([1, 0, 0]);
		var b = OperatorAiSkillRouter.Normalize([1, 0, 0, 0]);
		OperatorAiSkillRouter.Dot(a, b).Should().BeApproximately(1.0, 1e-6); // min length, no crash
		OperatorAiSkillRouter.Normalize([0, 0, 0, 0]).Should().Equal(0, 0, 0, 0); // zero vector → unchanged, no NaN
	}
}
