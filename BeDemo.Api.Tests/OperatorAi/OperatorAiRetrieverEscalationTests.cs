using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Zero-hit escalation ladder + pure-helper edge cases for <see cref="OperatorAiRetriever"/> (§6.1/§17.4/§17.8).
/// Complements <see cref="OperatorAiRetrieverTests"/> (happy path / cache). Covers the relaxed third attempt,
/// the `ZeroHitRetryAttempts` knob (0/1/2), SemanticSearch unavailability after readiness, an empty planner
/// fallback collapsing to ZeroHit, and the deterministic `CapAndDedupe` / `BuildEmbedCacheKey` helpers.
/// </summary>
public sealed class OperatorAiRetrieverEscalationTests
{
	private static readonly float[] Vector = [0.1f, 0.2f];

	private static (OperatorAiRetriever Retriever,
		Mock<ISearchWorkerKnowledgeClient> Knowledge,
		Mock<IOperatorAiPlannerFallbackSelector> Planner,
		Mock<IAiGrpcService> Ai) BuildReady(int zeroHitAttempts)
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(Vector, "nomic-embed-text", null));
		var knowledge = new Mock<ISearchWorkerKnowledgeClient>();
		knowledge.SetupGet(k => k.IsAvailable).Returns(true);
		var planner = new Mock<IOperatorAiPlannerFallbackSelector>();
		var status = new Mock<IOperatorAiKnowledgeStatusCache>();
		status.Setup(s => s.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

		var retriever = new OperatorAiRetriever(
			ai.Object, knowledge.Object, planner.Object, status.Object,
			new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 }),
			Options.Create(new AiServiceOptions { EmbeddingModel = "nomic-embed-text", EmbeddingDim = 2 }),
			Options.Create(new OperatorAiOptions
			{
				MaxSelectedBundleIndices = 4,
				MinRetrievalScore = 0.0,
				ZeroHitRetryAttempts = zeroHitAttempts,
				EmbedTimeoutMs = 2000,
				RetrievalTimeoutMs = 2000,
				QueryEmbeddingCacheTtlSeconds = 300,
				RetrievalRrfK = 60,
			}),
			NullLogger<OperatorAiRetriever>.Instance);
		return (retriever, knowledge, planner, ai);
	}

	private static SemanticSearchResponse Empty() => new() { Degraded = false };
	private static SemanticSearchResponse WithHit(int index) =>
		new SemanticSearchResponse { Degraded = false }.Also(r => r.Hits.Add(new SemanticSearchHit
		{ KnowledgeId = $"bundle:{index}", BundleIndex = index, SourceType = "stat_bundle", Score = 0.9 }));

	[Fact]
	public async Task Relaxed_retrieval_recovers_on_attempt_three()
	{
		var (retriever, knowledge, planner, _) = BuildReady(zeroHitAttempts: 2);
		// Initial SemanticSearch empty, planner empty (attempt 2), relaxed SemanticSearch returns a hit (attempt 3).
		knowledge.SetupSequence(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Empty())
			.ReturnsAsync(WithHit(7));
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<int>());

		var result = await retriever.RetrieveBundleIndicesAsync("oddly phrased question");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Relaxed);
		result.BundleIndices.Should().Equal(7);
	}

	[Fact]
	public async Task Zero_attempts_means_immediate_zero_hit_without_planner()
	{
		var (retriever, knowledge, planner, _) = BuildReady(zeroHitAttempts: 0);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Empty());

		var result = await retriever.RetrieveBundleIndicesAsync("nonsense");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.ZeroHit);
		planner.Verify(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task One_attempt_tries_planner_but_not_relaxed()
	{
		var (retriever, knowledge, planner, _) = BuildReady(zeroHitAttempts: 1);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Empty());
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<int>());

		var result = await retriever.RetrieveBundleIndicesAsync("nonsense");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.ZeroHit);
		planner.Verify(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
		// Only the initial SemanticSearch ran — no relaxed retrieval at attempts=1.
		knowledge.Verify(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once());
	}

	[Fact]
	public async Task SemanticSearch_null_after_ready_falls_back_to_planner()
	{
		var (retriever, knowledge, planner, _) = BuildReady(zeroHitAttempts: 2);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SemanticSearchResponse?)null); // ES went down between readiness and the query
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { 2 });

		var result = await retriever.RetrieveBundleIndicesAsync("how many users?");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Planner);
		result.BundleIndices.Should().Equal(2);
	}

	[Fact]
	public void CapAndDedupe_filters_range_dedupes_and_caps_preserving_order()
	{
		var capped = OperatorAiRetriever.CapAndDedupe(new[] { 2, 2, -1, 999, 5, 8, 5 }, topK: 3);
		capped.Should().Equal(2, 5, 8);
	}

	[Fact]
	public void BuildEmbedCacheKey_normalizes_and_is_model_scoped()
	{
		var a = OperatorAiRetriever.BuildEmbedCacheKey("  How   MANY  Users? ", "nomic-embed-text");
		var b = OperatorAiRetriever.BuildEmbedCacheKey("how many users?", "nomic-embed-text");
		var c = OperatorAiRetriever.BuildEmbedCacheKey("how many users?", "other-model");

		a.Should().Be(b, "trim + lowercase + collapsed whitespace must normalize to the same key");
		a.Should().Contain("how many users?");
		c.Should().NotBe(b, "the embed model version is part of the key (re-embed on model bump)");
	}
}

internal static class SemanticSearchResponseTestExtensions
{
	/// <summary>Tiny fluent helper so a response with hits can be built in one expression.</summary>
	public static SemanticSearchResponse Also(this SemanticSearchResponse r, Action<SemanticSearchResponse> mutate)
	{
		mutate(r);
		return r;
	}
}
