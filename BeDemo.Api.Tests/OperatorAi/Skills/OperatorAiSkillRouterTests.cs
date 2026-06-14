using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi.Skills;

/// <summary>
/// Edge cases for skill routing (operator-ai-skills v1, §4/§6): registry guarantees, the singleton vector cache
/// (warm-once + re-warm on model change), and the in-memory cosine router (route to the best skill; below-threshold
/// / embed-unavailable → general-assistant; SK-1/2/3/9). Uses fake skills whose descriptors embed to one-hot
/// vectors so cosine is deterministic.
/// </summary>
public sealed class OperatorAiSkillRouterTests
{
	/// <summary>Fake skill whose <see cref="Description"/> carries a marker that the test embedder maps to a one-hot vector.</summary>
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

	// Markers → one-hot vectors. A message with no marker embeds to a diffuse vector (cosine 0.5 with any one-hot).
	private static float[] EmbedFor(string text)
	{
		if (text.Contains("M0")) return [1, 0, 0, 0];
		if (text.Contains("M1")) return [0, 1, 0, 0];
		if (text.Contains("M2")) return [0, 0, 1, 0];
		if (text.Contains("Mgen")) return [0, 0, 0, 1];
		return [1, 1, 1, 1];
	}

	/// <summary>Decision helper that abstains (DetectSkill → null, IsBroadOverview → false) so the COSINE path is exercised in isolation.</summary>
	private static IOperatorAiDecisionHelper NoHelper() => Mock.Of<IOperatorAiDecisionHelper>();

	private static (OperatorAiSkillRouter Router, Mock<IAiGrpcService> Ai) BuildRouter(
		double threshold = 0.9, AiEmbedTextResult? forceEmbed = null)
	{
		var skills = new IOperatorAiSkill[]
		{
			new FakeSkill("stats", "M0"),
			new FakeSkill("reports", "M1"),
			new FakeSkill("moderation", "M2"),
			new FakeSkill(OperatorAiSkillRegistry.GeneralAssistantId, "Mgen"),
		};
		var registry = new OperatorAiSkillRegistry(skills);
		var cache = new OperatorAiSkillVectorCache();

		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string? _, CancellationToken __) => forceEmbed ?? new AiEmbedTextResult(EmbedFor(t), "model-a", null));

		var router = new OperatorAiSkillRouter(
			registry, cache, NoHelper(), ai.Object,
			new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
			Options.Create(new AiServiceOptions { EmbeddingModel = "model-a", EmbeddingDim = 4 }),
			Options.Create(new OperatorAiOptions { SkillRoutingMinScore = threshold, EmbedTimeoutMs = 2000 }),
			NullLogger<OperatorAiSkillRouter>.Instance);
		return (router, ai);
	}

	// PF-9 / O8 — embed-once: the router seeds the retriever's shared query-embedding cache with the RAW vector so
	// the stats skill does not embed the same message a second time.
	[Fact]
	public async Task PF9_router_seeds_retriever_query_embedding_cache()
	{
		var skills = new IOperatorAiSkill[]
		{
			new FakeSkill("stats", "M0"),
			new FakeSkill(OperatorAiSkillRegistry.GeneralAssistantId, "Mgen"),
		};
		using var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
		var ai = new Mock<IAiGrpcService>();
		var embedCalls = 0;
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string t, string? _, CancellationToken __) => { embedCalls++; return new AiEmbedTextResult(EmbedFor(t), "model-a", null); });

		var router = new OperatorAiSkillRouter(
			new OperatorAiSkillRegistry(skills), new OperatorAiSkillVectorCache(), NoHelper(), ai.Object, memoryCache,
			Options.Create(new AiServiceOptions { EmbeddingModel = "model-a", EmbeddingDim = 4 }),
			Options.Create(new OperatorAiOptions { SkillRoutingMinScore = 0.5, QueryEmbeddingCacheTtlSeconds = 300 }),
			NullLogger<OperatorAiSkillRouter>.Instance);

		await router.RouteAsync("M0 question");

		var key = BeDemo.Api.Services.OperatorAi.OperatorAiRetriever.BuildEmbedCacheKey("M0 question", "model-a");
		memoryCache.TryGetValue(key, out float[]? seeded).Should().BeTrue("the router seeds the retriever cache (embed-once)");
		seeded.Should().NotBeNull();
	}

	[Theory]
	[InlineData("tell me M0 please", "stats")]
	[InlineData("generate M1", "reports")]
	[InlineData("M2 backlog", "moderation")]
	public async Task Routes_to_the_best_matching_skill(string message, string expectedId)
	{
		var (router, _) = BuildRouter();
		var route = await router.RouteAsync(message);
		route.Skill.Id.Should().Be(expectedId);
		route.Fallback.Should().BeFalse();
		route.Score.Should().BeGreaterThanOrEqualTo(0.9);
	}

	[Fact]
	public async Task Below_threshold_falls_back_to_general_assistant()
	{
		var (router, _) = BuildRouter(threshold: 0.9);
		var route = await router.RouteAsync("a vague message with no marker"); // diffuse → cosine 0.5 < 0.9
		route.Skill.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
		route.Fallback.Should().BeTrue();
	}

	[Fact]
	public async Task Embed_unavailable_falls_back_to_general_assistant()
	{
		var (router, _) = BuildRouter(forceEmbed: new AiEmbedTextResult(null, null, "ollama down"));
		var route = await router.RouteAsync("M0 stats please");
		route.Skill.Id.Should().Be(OperatorAiSkillRegistry.GeneralAssistantId);
		route.Fallback.Should().BeTrue();
	}

	[Fact]
	public async Task Registry_requires_a_general_assistant_fallback()
	{
		var act = () => new OperatorAiSkillRegistry(new IOperatorAiSkill[] { new FakeSkill("stats", "M0") });
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Normalize_and_dot_compute_cosine()
	{
		var a = OperatorAiSkillRouter.Normalize([3, 0, 0, 0]);
		var b = OperatorAiSkillRouter.Normalize([1, 0, 0, 0]);
		OperatorAiSkillRouter.Dot(a, b).Should().BeApproximately(1.0, 1e-6);
		var c = OperatorAiSkillRouter.Normalize([0, 1, 0, 0]);
		OperatorAiSkillRouter.Dot(a, c).Should().BeApproximately(0.0, 1e-6);
	}

	[Fact]
	public async Task Vector_cache_warms_once_and_rewarms_on_model_change()
	{
		var cache = new OperatorAiSkillVectorCache();
		var calls = 0;
		Func<string, CancellationToken, Task<float[]?>> embed = (t, _) =>
		{
			Interlocked.Increment(ref calls);
			return Task.FromResult<float[]?>(EmbedFor(t));
		};
		var descriptors = new List<(string, string)> { ("a", "M0"), ("b", "M1") };

		await cache.GetOrWarmAsync(descriptors, "model-a", embed, default);
		await cache.GetOrWarmAsync(descriptors, "model-a", embed, default);
		calls.Should().Be(2, "warmed once for 2 descriptors; the second call hits the cache");

		await cache.GetOrWarmAsync(descriptors, "model-b", embed, default); // model changed → re-warm
		calls.Should().Be(4);
	}

	[Fact]
	public async Task Vector_cache_returns_null_when_all_embeds_fail()
	{
		var cache = new OperatorAiSkillVectorCache();
		var result = await cache.GetOrWarmAsync(
			new List<(string, string)> { ("a", "x") }, "model-a", (_, _) => Task.FromResult<float[]?>(null), default);
		result.Should().BeNull();
	}
}
