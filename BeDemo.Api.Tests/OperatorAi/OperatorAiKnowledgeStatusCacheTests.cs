using BeDemo.Api.Configuration;
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
/// Edge cases for the readiness gate cache (§17.4 / RT-22). Readiness = worker `Ready` flag AND the deployed
/// embed model + dimension match our single source of truth; a drift means the index is stale for our config and
/// retrieval must fall back to the planner. Also covers the short-TTL caching that avoids a worker round trip on
/// every operator turn.
/// </summary>
public sealed class OperatorAiKnowledgeStatusCacheTests
{
	private static OperatorAiKnowledgeStatusCache Build(Mock<ISearchWorkerKnowledgeClient> knowledge, out IMemoryCache cache)
	{
		cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 });
		var ai = Options.Create(new AiServiceOptions { EmbeddingModel = "nomic-embed-text", EmbeddingDim = 768 });
		var opt = Options.Create(new OperatorAiOptions { KnowledgeStatusCacheTtlSeconds = 30 });
		return new OperatorAiKnowledgeStatusCache(knowledge.Object, cache, ai, opt, NullLogger<OperatorAiKnowledgeStatusCache>.Instance);
	}

	private static KnowledgeIndexStatusResponse Status(bool ready, string model = "nomic-embed-text", int dim = 768)
		=> new() { Ready = ready, EmbedModelVersion = model, VectorDim = dim, DocCount = 61, ExpectedDocCount = 61 };

	[Fact]
	public async Task Ready_with_matching_model_and_dim_is_ready()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(true));

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeTrue();
	}

	[Fact]
	public async Task Model_drift_is_not_ready()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(true, model: "some-other-embed-model"));

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeFalse("a model drift means the vectors are stale for our config");
	}

	[Fact]
	public async Task Dim_drift_is_not_ready()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(true, dim: 384));

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task Not_ready_flag_is_not_ready()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(false));

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task Worker_unavailable_is_not_ready()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(false); // search disabled → no probe at all

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeFalse();
		k.Verify(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Status_is_cached_and_force_refresh_bypasses()
	{
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(true));

		var cache = Build(k, out _);

		await cache.GetStatusAsync();         // miss → worker call
		await cache.GetStatusAsync();         // hit → no worker call
		k.Verify(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()), Times.Once());

		await cache.GetStatusAsync(forceRefresh: true); // bypass → second worker call
		k.Verify(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	[Fact]
	public async Task Empty_model_or_zero_dim_from_worker_does_not_block_readiness()
	{
		// Tolerant matching: a worker that does not report model/dim (empty/0) must not be treated as drift.
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.KnowledgeIndexStatusAsync(It.IsAny<KnowledgeIndexStatusRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Status(true, model: "", dim: 0));

		var cache = Build(k, out _);
		(await cache.IsReadyAsync()).Should().BeTrue();
	}
}
