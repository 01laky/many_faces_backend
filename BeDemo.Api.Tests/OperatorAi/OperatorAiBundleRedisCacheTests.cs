using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class OperatorAiBundleRedisCacheTests
{
	private static OperatorAiBundleRedisCache CreateCache(InMemoryOperatorAiRedisStringStore store) =>
		new(
			store,
			Options.Create(new OperatorAiOptions
			{
				LiveBundleCacheLockSeconds = 30,
				LiveBundleCacheWaitPollMilliseconds = 10,
			}),
			NullLogger<OperatorAiBundleRedisCache>.Instance);

	[Fact]
	public async Task GetOrLoadAsync_cache_hit_does_not_call_loader()
	{
		var store = new InMemoryOperatorAiRedisStringStore();
		var catalogVersion = OperatorAiEntityBundleCatalog.CatalogVersion;
		await store.SetWithExpiryMillisecondsAsync(
			OperatorAiBundleRedisCache.BuildBundleKey(catalogVersion, 3),
			"{\"bundleId\":\"messages\"}",
			300_000);

		var cache = CreateCache(store);
		var loaderCalls = 0;
		var result = await cache.GetOrLoadAsync(
			3,
			300_000,
			_ =>
			{
				loaderCalls++;
				return Task.FromResult("fresh");
			},
			TimeSpan.FromSeconds(5));

		result.Success.Should().BeTrue();
		result.CacheHit.Should().BeTrue();
		result.Json.Should().Be("{\"bundleId\":\"messages\"}");
		loaderCalls.Should().Be(0);
	}

	[Fact]
	public async Task GetOrLoadAsync_miss_calls_loader_and_sets_value()
	{
		var store = new InMemoryOperatorAiRedisStringStore();
		var cache = CreateCache(store);
		var result = await cache.GetOrLoadAsync(
			5,
			300_000,
			_ => Task.FromResult("{\"index\":5}"),
			TimeSpan.FromSeconds(5));

		result.Success.Should().BeTrue();
		result.CacheHit.Should().BeFalse();
		var key = OperatorAiBundleRedisCache.BuildBundleKey(OperatorAiEntityBundleCatalog.CatalogVersion, 5);
		(await store.GetAsync(key)).Should().Be("{\"index\":5}");
	}

	[Fact]
	public async Task GetOrLoadAsync_concurrent_misses_call_loader_once()
	{
		var store = new InMemoryOperatorAiRedisStringStore();
		var cache = CreateCache(store);
		var loaderCalls = 0;
		var gate = new SemaphoreSlim(0, 2);

		Task<OperatorAiBundleRedisLoadResult> LoadAsync() =>
			cache.GetOrLoadAsync(
				7,
				300_000,
				async _ =>
				{
					Interlocked.Increment(ref loaderCalls);
					await gate.WaitAsync(TimeSpan.FromSeconds(2));
					return "{\"index\":7}";
				},
				TimeSpan.FromSeconds(5));

		var t1 = LoadAsync();
		await Task.Delay(30);
		var t2 = LoadAsync();
		gate.Release(2);

		var results = await Task.WhenAll(t1, t2);
		results.Should().AllSatisfy(r =>
		{
			r.Success.Should().BeTrue();
			r.Json.Should().Be("{\"index\":7}");
		});
		loaderCalls.Should().Be(1);
	}

	[Fact]
	public async Task GetOrLoadAsync_loader_failure_does_not_cache()
	{
		var store = new InMemoryOperatorAiRedisStringStore();
		var cache = CreateCache(store);
		var result = await cache.GetOrLoadAsync(
			9,
			300_000,
			_ => throw new InvalidOperationException("db down"),
			TimeSpan.FromSeconds(1));

		result.Success.Should().BeFalse();
		var key = OperatorAiBundleRedisCache.BuildBundleKey(OperatorAiEntityBundleCatalog.CatalogVersion, 9);
		(await store.GetAsync(key)).Should().BeNull();
	}

	[Fact]
	public async Task BuildBundleKey_includes_catalog_version()
	{
		var key = OperatorAiBundleRedisCache.BuildBundleKey(2, 10);
		key.Should().Be("bedemo:operator-ai:live-bundle:v2:idx:10");
	}
}
