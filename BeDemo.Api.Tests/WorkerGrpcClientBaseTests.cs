using BeDemo.Api.Services;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Verifies the channel-cache and disposal skeleton in <see cref="WorkerGrpcClientBase{TClient}"/> (Phase 2 dedup):
/// same cache-key → same client instance without rebuilding; different key → stale channel disposed + new client
/// built; Dispose is idempotent.
/// </summary>
[Trait("Category", "BackendInfra")]
public sealed class WorkerGrpcClientBaseTests
{
	private sealed record TestClient(string Id);

	/// <summary>Minimal test double: wraps GetOrReplaceClient and counts channel builds.</summary>
	private sealed class TestWorkerClient : WorkerGrpcClientBase<TestClient>
	{
		private int _buildCount;
		public int BuildCount => _buildCount;

		// Uses a plain http:// URL so GrpcWorkerChannelFactory.CreateChannel creates a lazy channel (no real server needed).
		public TestClient Fetch(string cacheKey, string url = "http://localhost:59987") =>
			GetOrReplaceClient(
				cacheKey,
				() => GrpcWorkerChannelFactory.CreateChannel(
					new GrpcWorkerChannelFactory.GrpcWorkerTlsSettings(url, null, null, null, null, "Test"),
					CertificatesToDispose),
				_ => { Interlocked.Increment(ref _buildCount); return new TestClient("client:" + cacheKey); });
	}

	[Fact]
	public void SameCacheKey_ReturnsSameClientInstance_WithoutRebuild()
	{
		using var sut = new TestWorkerClient();
		var first = sut.Fetch("key1");
		var second = sut.Fetch("key1");
		second.Should().BeSameAs(first);
		sut.BuildCount.Should().Be(1);
	}

	[Fact]
	public void DifferentCacheKey_BuildsNewClient()
	{
		using var sut = new TestWorkerClient();
		var a = sut.Fetch("key-a");
		var b = sut.Fetch("key-b");
		b.Should().NotBeSameAs(a);
		sut.BuildCount.Should().Be(2);
	}

	[Fact]
	public void CacheKey_ChangedBack_RebuildsDontReturnEvictedInstance()
	{
		using var sut = new TestWorkerClient();
		var first = sut.Fetch("k1");
		_ = sut.Fetch("k2");    // evicts k1 channel
		var third = sut.Fetch("k1");
		third.Should().NotBeSameAs(first, "k1 channel was evicted and disposed; it must be rebuilt as a new instance");
		sut.BuildCount.Should().Be(3);
	}

	[Fact]
	public void Dispose_IsIdempotent_DoesNotThrow()
	{
		var sut = new TestWorkerClient();
		sut.Fetch("k1");
		sut.Dispose();
		var act = () => sut.Dispose();
		act.Should().NotThrow();
	}

	[Fact]
	public void Dispose_WithNoActiveChannel_DoesNotThrow()
	{
		var act = () =>
		{
			using var sut = new TestWorkerClient();
		};
		act.Should().NotThrow();
	}
}
