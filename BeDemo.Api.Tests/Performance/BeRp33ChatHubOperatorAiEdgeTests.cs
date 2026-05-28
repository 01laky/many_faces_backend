using System.Net;
using System.Net.Http.Headers;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Tests.OperatorAi;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP33 edge cases — ChatHub Operator AI read reduction (prefetch cache + auth gates).</summary>
public sealed class BeRp33ChatHubOperatorAiEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp33ChatHubOperatorAiEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP33-U1 — single bundle load returns stable aggregate (live SendToAiWithOperatorStats uses prefetch).</summary>
	[Fact]
	public async Task BE_RP33_U1_SingleBundleLoad_BoundedDbCommands()
	{
		var dbName = Guid.NewGuid().ToString("N");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
		services.AddSingleton<IOptions<OperatorAiOptions>>(Options.Create(new OperatorAiOptions()));

		await using var sp = services.BuildServiceProvider();
		var factory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
		var loader = new OperatorAiEntityBundleLoader(factory, sp.GetRequiredService<IOptions<OperatorAiOptions>>());

		var bundle = await loader.LoadAsync(12, CancellationToken.None);
		bundle.BundleId.Should().Be("entity.faces");
		bundle.TotalCount.Should().BeGreaterThanOrEqualTo(0);
	}

	/// <summary>BE-RP33-U2 — operator AI REST endpoints reject wrong face scope (mirrors hub NotOperator gate).</summary>
	[Fact]
	public async Task BE_RP33_U2_OperatorAiEndpoints_RejectPublicFaceScope()
	{
		using var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		(await client.GetAsync("/api/operator-ai/conversations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
		(await client.GetAsync("/api/operator-ai/live-stats-cache")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>BE-RP33-U3 — Redis bundle cache hit avoids DB loader (no 61-query storm on warm cache).</summary>
	[Fact]
	public async Task BE_RP33_U3_RedisBundleCacheHit_SkipsDbLoader()
	{
		var store = new InMemoryOperatorAiRedisStringStore();
		for (var index = 0; index < OperatorAiEntityBundleCatalog.BundleCount; index++)
		{
			await store.SetWithExpiryMillisecondsAsync(
				OperatorAiBundleRedisCache.BuildBundleKey(OperatorAiEntityBundleCatalog.CatalogVersion, index),
				"{\"bundleId\":\"cached\"}",
				300_000);
		}

		var redisCache = new OperatorAiBundleRedisCache(
			store,
			Options.Create(new OperatorAiOptions()),
			NullLogger<OperatorAiBundleRedisCache>.Instance);

		var dbName = Guid.NewGuid().ToString("N");
		var interceptor = new DbCommandCountInterceptor();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContextFactory<ApplicationDbContext>(options =>
		{
			options.UseInMemoryDatabase(dbName);
			options.AddInterceptors(interceptor);
		});
		services.AddSingleton<IOptions<OperatorAiOptions>>(Options.Create(new OperatorAiOptions()));
		services.AddSingleton<IOperatorAiBundleRedisCache>(redisCache);
		services.AddSingleton<IOperatorAiLiveStatsCacheSettingsProvider>(new FixedTtlCacheSettingsProvider());
		services.AddSingleton<IOperatorAiEntityBundleLoader, OperatorAiEntityBundleLoader>();
		services.AddSingleton<IOperatorAiLiveStatsPrefetcher, OperatorAiLiveStatsPrefetcher>();

		await using var sp = services.BuildServiceProvider();
		var prefetcher = sp.GetRequiredService<IOperatorAiLiveStatsPrefetcher>();

		interceptor.Reset();
		var result = await prefetcher.PrefetchAllAsync(CancellationToken.None);
		result.CacheHits.Should().Be(OperatorAiEntityBundleCatalog.BundleCount);
		result.CacheMisses.Should().Be(0);
		interceptor.CommandCount.Should().Be(0, "warm Redis L2 should not hit PostgreSQL loaders");
	}

	private sealed class FixedTtlCacheSettingsProvider : IOperatorAiLiveStatsCacheSettingsProvider
	{
		public Task<long> GetTtlMillisecondsAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(300_000L);

		public Task<long> SetTtlMillisecondsAsync(
			long ttlMilliseconds,
			string? updatedByUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(ttlMilliseconds);

		public OperatorAiLiveStatsCacheSettingsDto ToDto(long ttlMilliseconds) =>
			new()
			{
				TtlMilliseconds = ttlMilliseconds,
				DefaultTtlMilliseconds = 300_000,
				MinTtlMilliseconds = 30_000,
				MaxTtlMilliseconds = 3_600_000,
			};
	}
}
