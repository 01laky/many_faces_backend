using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class OperatorAiPublicStatsSettingsServiceTests
{
	private static (OperatorAiPublicStatsSettingsService Svc, string DbName) CreateService()
	{
		var dbName = Guid.NewGuid().ToString();
		var services = new ServiceCollection();
		services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
		services.AddMemoryCache();
		services.Configure<OperatorAiOptions>(o => o.LiveBundleCacheSettingsMemoryCacheSeconds = 30);
		var provider = services.BuildServiceProvider();
		var svc = new OperatorAiPublicStatsSettingsService(
			provider.GetRequiredService<IServiceScopeFactory>(),
			provider.GetRequiredService<IMemoryCache>(),
			provider.GetRequiredService<IOptions<OperatorAiOptions>>());
		return (svc, dbName);
	}

	private static async Task SeedAsync(string dbName, string mode, int parallel)
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;
		await using var db = new ApplicationDbContext(options);
		db.OperatorAiPublicStatsSettings.Add(new OperatorAiPublicStatsSettings
		{
			Id = 1,
			PublicStatsMode = mode,
			LiveMaxParallelBundleCalls = parallel,
			UpdatedAtUtc = DateTime.UtcNow,
		});
		await db.SaveChangesAsync();
	}

	[Fact]
	public async Task GetAsync_caches_db_read_in_l1()
	{
		var (svc, dbName) = CreateService();
		await SeedAsync(dbName, "live", 4);

		var first = await svc.GetAsync();
		await using (var db = new ApplicationDbContext(
						 new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options))
		{
			var row = await db.OperatorAiPublicStatsSettings.SingleAsync();
			row.PublicStatsMode = "off";
			row.LiveMaxParallelBundleCalls = 1;
			await db.SaveChangesAsync();
		}

		var second = await svc.GetAsync();
		first.PublicStatsMode.Should().Be("live");
		first.LiveMaxParallelBundleCalls.Should().Be(4);
		second.Should().BeEquivalentTo(first);
	}

	[Fact]
	public async Task SetAsync_updates_l1_immediately()
	{
		var (svc, dbName) = CreateService();
		var saved = await svc.SetAsync(new OperatorAiPublicStatsSettingsValues("live", 5), "user-1");
		saved.PublicStatsMode.Should().Be("live");
		saved.LiveMaxParallelBundleCalls.Should().Be(5);

		var readBack = await svc.GetAsync();
		readBack.Should().BeEquivalentTo(saved);

		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;
		await using var db = new ApplicationDbContext(options);
		var row = await db.OperatorAiPublicStatsSettings.AsNoTracking().SingleAsync();
		row.PublicStatsMode.Should().Be("live");
		row.LiveMaxParallelBundleCalls.Should().Be(5);
	}

	[Fact]
	public async Task GetAsync_falls_back_to_defaults_when_row_missing()
	{
		var (svc, _) = CreateService();
		var values = await svc.GetAsync();
		values.PublicStatsMode.Should().Be(OperatorAiPublicStatsConstraints.DefaultPublicStatsMode);
		values.LiveMaxParallelBundleCalls.Should().Be(OperatorAiPublicStatsConstraints.DefaultLiveMaxParallelBundleCalls);
	}
}
