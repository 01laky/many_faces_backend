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
/// Edge cases for the knowledge indexer (§7/§8/§17.5): single-flight coalescing (RT-17), content-hash
/// idempotency (RT-7), and the worker/embed failure paths that must NOT persist the marker so a later run retries.
/// Uses a dictionary-backed Redis fake + mocked embedder/worker — no real Ollama or Elasticsearch.
/// </summary>
public sealed class OperatorAiKnowledgeIndexerTests
{
	private static readonly float[] Vector = new float[768];

	private sealed class FakeRedis : IOperatorAiRedisStringStore
	{
		private readonly Dictionary<string, string> _data = new();
		public bool Contains(string key) => _data.ContainsKey(key);
		public Task<string?> GetAsync(string key, CancellationToken ct = default)
			=> Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);
		public Task<bool> SetNotExistsAsync(string key, string value, int expirySeconds, CancellationToken ct = default)
		{
			if (_data.ContainsKey(key)) return Task.FromResult(false);
			_data[key] = value;
			return Task.FromResult(true);
		}
		public Task SetWithExpiryMillisecondsAsync(string key, string value, long expiryMilliseconds, CancellationToken ct = default)
		{
			_data[key] = value;
			return Task.CompletedTask;
		}
		public Task<bool> CompareAndDeleteAsync(string key, string token, CancellationToken ct = default)
		{
			if (_data.TryGetValue(key, out var v) && v == token) { _data.Remove(key); return Task.FromResult(true); }
			return Task.FromResult(false);
		}
	}

	private static OperatorAiKnowledgeIndexer Build(
		Mock<IAiGrpcService> ai,
		Mock<ISearchWorkerKnowledgeClient> knowledge,
		IOperatorAiRedisStringStore? redis = null,
		IOperatorAiAnswerCache? answerCache = null)
		=> new(
			ai.Object, knowledge.Object,
			Options.Create(new AiServiceOptions { EmbeddingModel = "nomic-embed-text", EmbeddingDim = 768 }),
			Options.Create(new OperatorAiOptions()),
			NullLogger<OperatorAiKnowledgeIndexer>.Instance,
			answerCache
				?? new OperatorAiAnswerCache(
					new MemoryCache(new MemoryCacheOptions()),
					Options.Create(new OperatorAiOptions())),
			redis);

	private static Mock<IAiGrpcService> EmbedAlwaysOk()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(Vector, "nomic-embed-text", null));
		return ai;
	}

	[Fact]
	public async Task Worker_unavailable_skips_without_embedding()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(false);

		var result = await Build(ai, k).RebuildAsync(force: true);

		result.Skipped.Should().BeTrue();
		result.Error.Should().Be("search_worker_unavailable");
		ai.Verify(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Embed_unavailable_does_not_index_and_does_not_persist_marker()
	{
		var ai = new Mock<IAiGrpcService>();
		ai.Setup(a => a.EmbedTextAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AiEmbedTextResult(null, null, "ollama down")); // every bundle fails to embed
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		var redis = new FakeRedis();

		var result = await Build(ai, k, redis).RebuildAsync(force: true);

		result.Error.Should().Be("embed_unavailable");
		result.IndexedCount.Should().Be(0);
		k.Verify(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()), Times.Never());
		redis.Contains(OperatorAiKnowledgeIndexer.ContentHashMarkerKey).Should().BeFalse();
	}

	[Fact]
	public async Task Successful_rebuild_indexes_all_61_and_persists_marker()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeRequest req, CancellationToken _) =>
				new IndexKnowledgeResponse { IndexedCount = req.Documents.Count, FailedCount = 0 });
		var redis = new FakeRedis();

		var result = await Build(ai, k, redis).RebuildAsync(force: true);

		result.IndexedCount.Should().Be(OperatorAiEntityBundleCatalog.BundleCount);
		result.FailedCount.Should().Be(0);
		result.Error.Should().BeNull();
		redis.Contains(OperatorAiKnowledgeIndexer.ContentHashMarkerKey).Should().BeTrue("a fully-successful upsert persists the content-hash marker");
	}

	[Fact]
	public async Task Successful_rebuild_flushes_the_operator_ai_answer_cache()
	{
		// A reindex changes the underlying bundle data; cached answers (keyed on message text only) must be
		// dropped so a re-asked question can't be served a stale count.
		var answerCache = new OperatorAiAnswerCache(
			new MemoryCache(new MemoryCacheOptions()),
			Options.Create(new OperatorAiOptions { AnswerCacheEnabled = true }));
		answerCache.Set("stats", "how many users", "cached-stale-answer");
		answerCache.TryGet("stats", "how many users", out _).Should().BeTrue();

		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeRequest req, CancellationToken _) =>
				new IndexKnowledgeResponse { IndexedCount = req.Documents.Count, FailedCount = 0 });

		await Build(ai, k, new FakeRedis(), answerCache).RebuildAsync(force: true);

		answerCache
			.TryGet("stats", "how many users", out _)
			.Should()
			.BeFalse("a successful reindex flushes cached operator-AI answers");
	}

	[Fact]
	public async Task Partial_failure_rebuild_still_flushes_the_answer_cache()
	{
		// Even a partial upsert changed the index, so stale cached answers must still be dropped.
		var answerCache = new OperatorAiAnswerCache(
			new MemoryCache(new MemoryCacheOptions()),
			Options.Create(new OperatorAiOptions { AnswerCacheEnabled = true }));
		answerCache.Set("stats", "how many users", "cached-stale-answer");

		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeRequest req, CancellationToken _) =>
				new IndexKnowledgeResponse { IndexedCount = req.Documents.Count - 1, FailedCount = 1 });

		var result = await Build(ai, k, new FakeRedis(), answerCache).RebuildAsync(force: true);

		result.FailedCount.Should().BeGreaterThan(0);
		answerCache
			.TryGet("stats", "how many users", out _)
			.Should()
			.BeFalse("a partial reindex still changed the index, so cached answers are flushed");
	}

	[Fact]
	public async Task Search_worker_unavailable_does_not_flush_the_answer_cache()
	{
		// The reindex never ran (worker down), so nothing changed — cached answers must be left intact.
		var answerCache = new OperatorAiAnswerCache(
			new MemoryCache(new MemoryCacheOptions()),
			Options.Create(new OperatorAiOptions { AnswerCacheEnabled = true }));
		answerCache.Set("stats", "how many users", "cached-answer");

		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(false);

		var result = await Build(ai, k, new FakeRedis(), answerCache).RebuildAsync(force: true);

		result.Skipped.Should().BeTrue();
		answerCache
			.TryGet("stats", "how many users", out var hit)
			.Should()
			.BeTrue("a no-op rebuild must not flush the cache");
		hit.Should().Be("cached-answer");
	}

	[Fact]
	public async Task Force_false_skips_when_content_hash_unchanged()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeRequest req, CancellationToken _) =>
				new IndexKnowledgeResponse { IndexedCount = req.Documents.Count, FailedCount = 0 });
		var redis = new FakeRedis();
		var indexer = Build(ai, k, redis);

		await indexer.RebuildAsync(force: true);            // first build sets the marker
		var second = await indexer.RebuildAsync(force: false); // hash unchanged → idempotent skip

		second.Skipped.Should().BeTrue();
		k.Verify(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()), Times.Once());
	}

	[Fact]
	public async Task Partial_failure_reports_and_does_not_persist_marker()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeRequest req, CancellationToken _) =>
				new IndexKnowledgeResponse { IndexedCount = req.Documents.Count - 1, FailedCount = 1 });
		var redis = new FakeRedis();

		var result = await Build(ai, k, redis).RebuildAsync(force: true);

		result.FailedCount.Should().Be(1);
		result.Error.Should().Be("partial_failure");
		redis.Contains(OperatorAiKnowledgeIndexer.ContentHashMarkerKey).Should().BeFalse("partial failures must retry on the next run");
	}

	[Fact]
	public async Task Null_index_response_reports_worker_unavailable()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IndexKnowledgeResponse?)null);

		var result = await Build(ai, k).RebuildAsync(force: true);

		result.Error.Should().Be("search_worker_unavailable");
	}

	[Fact]
	public async Task Concurrent_rebuild_coalesces_via_single_flight()
	{
		var ai = EmbedAlwaysOk();
		var k = new Mock<ISearchWorkerKnowledgeClient>();
		k.SetupGet(x => x.IsAvailable).Returns(true);

		// Gate the index call so the first rebuild holds the single-flight gate while the second arrives.
		var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		k.Setup(x => x.IndexKnowledgeAsync(It.IsAny<IndexKnowledgeRequest>(), It.IsAny<CancellationToken>()))
			.Returns(async (IndexKnowledgeRequest req, CancellationToken _) =>
			{
				await gate.Task;
				return new IndexKnowledgeResponse { IndexedCount = req.Documents.Count, FailedCount = 0 };
			});

		var indexer = Build(ai, k); // no Redis → process semaphore only

		var first = indexer.RebuildAsync(force: true);
		// Spin until the first call is inside the gate (IsRebuildInProgress) before issuing the second.
		var spins = 0;
		while (!indexer.IsRebuildInProgress && spins++ < 1000)
			await Task.Delay(2);

		var second = await indexer.RebuildAsync(force: true); // should coalesce immediately
		second.Coalesced.Should().BeTrue();
		second.Error.Should().Be("reindex_already_running");

		gate.SetResult();
		var firstResult = await first;
		firstResult.Coalesced.Should().BeFalse();
		firstResult.IndexedCount.Should().Be(OperatorAiEntityBundleCatalog.BundleCount);
	}
}
