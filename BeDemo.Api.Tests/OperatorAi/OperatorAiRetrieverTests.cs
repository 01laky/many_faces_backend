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
/// Unit tests for the RAG selection step (<see cref="OperatorAiRetriever"/>). Covers the decision ladder:
/// RAG happy path + deterministic ordering (RT-1), cold-start readiness → planner fallback (RT-15), embed/ES
/// down → planner, zero-hit escalation (§6.1), and the query-embedding cache (RT-21). The worker, embedder,
/// planner, and status cache are mocked; values are never produced here (correctness rule §4 — selection only).
/// </summary>
public sealed class OperatorAiRetrieverTests
{
	private static readonly float[] Vector = [0.11f, 0.22f, 0.33f];

	private static (OperatorAiRetriever Retriever,
		Mock<IAiGrpcService> Ai,
		Mock<ISearchWorkerKnowledgeClient> Knowledge,
		Mock<IOperatorAiPlannerFallbackSelector> Planner,
		Mock<IOperatorAiKnowledgeStatusCache> Status) Build()
	{
		var ai = new Mock<IAiGrpcService>();
		var knowledge = new Mock<ISearchWorkerKnowledgeClient>();
		var planner = new Mock<IOperatorAiPlannerFallbackSelector>();
		var status = new Mock<IOperatorAiKnowledgeStatusCache>();

		var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });

		var aiOptions = Options.Create(new AiServiceOptions { EmbeddingModel = "nomic-embed-text", EmbeddingDim = 3 });
		var options = Options.Create(new OperatorAiOptions
		{
			MaxSelectedBundleIndices = 4,
			MinRetrievalScore = 0.0,
			ZeroHitRetryAttempts = 2,
			EmbedTimeoutMs = 2000,
			RetrievalTimeoutMs = 2000,
			QueryEmbeddingCacheTtlSeconds = 300,
			RetrievalRrfK = 60,
		});

		var retriever = new OperatorAiRetriever(
			ai.Object, knowledge.Object, planner.Object, status.Object, cache,
			aiOptions, options, NullLogger<OperatorAiRetriever>.Instance);

		return (retriever, ai, knowledge, planner, status);
	}

	private static void SetupReadyWithEmbed(Mock<ISearchWorkerKnowledgeClient> knowledge, Mock<IOperatorAiKnowledgeStatusCache> status, Mock<IAiGrpcService> ai)
	{
		knowledge.SetupGet(k => k.IsAvailable).Returns(true);
		status.Setup(s => s.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(Vector, "nomic-embed-text", null));
	}

	private static SemanticSearchResponse Response(bool degraded, params (string Id, int Index, double Score)[] hits)
	{
		var resp = new SemanticSearchResponse { Degraded = degraded };
		foreach (var (id, index, score) in hits)
		{
			resp.Hits.Add(new SemanticSearchHit
			{
				KnowledgeId = id,
				BundleIndex = index,
				SourceType = "stat_bundle",
				Score = score,
			});
		}
		return resp;
	}

	[Fact]
	public async Task Rag_happy_path_orders_hits_by_score_then_index()
	{
		var (retriever, ai, knowledge, _, status) = Build();
		SetupReadyWithEmbed(knowledge, status, ai);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response(false, ("bundle:a", 5, 0.90), ("bundle:b", 2, 0.95), ("bundle:c", 8, 0.95)));

		var result = await retriever.RetrieveBundleIndicesAsync("how many albums are pending?");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Rag);
		// score desc (0.95, 0.95, 0.90), tie broken by bundle_index asc (2 before 8).
		result.BundleIndices.Should().Equal(2, 8, 5);
		result.IsZeroHit.Should().BeFalse();
	}

	[Fact]
	public async Task Index_not_ready_falls_back_to_planner_without_embedding()
	{
		var (retriever, ai, knowledge, planner, status) = Build();
		knowledge.SetupGet(k => k.IsAvailable).Returns(true);
		status.Setup(s => s.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false); // cold start
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { 3, 7 });

		var result = await retriever.RetrieveBundleIndicesAsync("platform overview");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Planner);
		result.BundleIndices.Should().Equal(3, 7);
		// RT-15: never a zero-hit refusal due to an unbuilt index; embedding is not even attempted.
		ai.Verify(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Embed_unavailable_falls_back_to_planner()
	{
		var (retriever, ai, knowledge, planner, status) = Build();
		knowledge.SetupGet(k => k.IsAvailable).Returns(true);
		status.Setup(s => s.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(null, null, "ollama unavailable"));
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { 1 });

		var result = await retriever.RetrieveBundleIndicesAsync("how many users?");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Planner);
		result.BundleIndices.Should().Equal(1);
	}

	[Fact]
	public async Task Zero_hit_escalates_to_planner_attempt_two()
	{
		var (retriever, ai, knowledge, planner, status) = Build();
		SetupReadyWithEmbed(knowledge, status, ai);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response(false)); // no hits
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { 4 });

		var result = await retriever.RetrieveBundleIndicesAsync("something vague");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.Planner);
		result.BundleIndices.Should().Equal(4);
	}

	[Fact]
	public async Task Zero_hit_after_all_attempts_returns_zero_hit_strategy()
	{
		var (retriever, ai, knowledge, planner, status) = Build();
		SetupReadyWithEmbed(knowledge, status, ai);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response(false)); // empty on both the initial and relaxed search
		planner.Setup(p => p.SelectBundleIndicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<int>()); // planner also finds nothing

		var result = await retriever.RetrieveBundleIndicesAsync("what is the weather today");

		result.Strategy.Should().Be(OperatorAiSelectionStrategy.ZeroHit);
		result.IsZeroHit.Should().BeTrue();
		result.BundleIndices.Should().BeEmpty();
	}

	[Fact]
	public async Task Query_embedding_is_cached_across_identical_messages()
	{
		var (retriever, ai, knowledge, _, status) = Build();
		SetupReadyWithEmbed(knowledge, status, ai);
		knowledge.Setup(k => k.SemanticSearchAsync(It.IsAny<SemanticSearchRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response(false, ("bundle:a", 0, 0.9)));

		var first = await retriever.RetrieveBundleIndicesAsync("How many USERS?  ");
		var second = await retriever.RetrieveBundleIndicesAsync("how many users?"); // normalizes to the same key

		first.EmbedCacheHit.Should().BeFalse();
		second.EmbedCacheHit.Should().BeTrue();
		// RT-21: the embedder is invoked exactly once for the two normalized-identical questions.
		ai.Verify(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once());
	}

	[Fact]
	public void FilterAndOrderHits_filters_orders_dedupes_and_caps()
	{
		var hits = new[]
		{
			new SemanticSearchHit { KnowledgeId = "k1", BundleIndex = 5, SourceType = "stat_bundle", Score = 0.50 },
			new SemanticSearchHit { KnowledgeId = "k2", BundleIndex = 2, SourceType = "stat_bundle", Score = 0.95 },
			new SemanticSearchHit { KnowledgeId = "k3", BundleIndex = 2, SourceType = "stat_bundle", Score = 0.80 }, // dup index → dropped
			new SemanticSearchHit { KnowledgeId = "k4", BundleIndex = 8, SourceType = "stat_bundle", Score = 0.95 },
			new SemanticSearchHit { KnowledgeId = "k5", BundleIndex = 9, SourceType = "doc", Score = 0.99 },          // wrong source → dropped
			new SemanticSearchHit { KnowledgeId = "k6", BundleIndex = -1, SourceType = "stat_bundle", Score = 0.99 }, // negative → dropped
			new SemanticSearchHit { KnowledgeId = "k7", BundleIndex = 3, SourceType = "stat_bundle", Score = 0.10 },  // below min → dropped
		};

		var ordered = OperatorAiRetriever.FilterAndOrderHits(hits, minScore: 0.3, topK: 4);

		ordered.Select(h => h.BundleIndex).Should().Equal(2, 8, 5);
	}
}
