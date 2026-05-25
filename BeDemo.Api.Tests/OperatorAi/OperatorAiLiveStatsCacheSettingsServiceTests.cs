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

public sealed class OperatorAiLiveStatsCacheSettingsServiceTests
{
	private static (OperatorAiLiveStatsCacheSettingsService Svc, string DbName) CreateService()
	{
		var dbName = Guid.NewGuid().ToString();
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;

		var services = new ServiceCollection();
		services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
		services.AddMemoryCache();
		services.Configure<OperatorAiOptions>(o => o.LiveBundleCacheSettingsMemoryCacheSeconds = 30);
		var provider = services.BuildServiceProvider();
		var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
		var memory = provider.GetRequiredService<IMemoryCache>();
		var svc = new OperatorAiLiveStatsCacheSettingsService(
			scopeFactory,
			memory,
			provider.GetRequiredService<IOptions<OperatorAiOptions>>());
		return (svc, dbName);
	}

	private static async Task SeedTtlAsync(string dbName, long ttlMs)
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;
		await using var db = new ApplicationDbContext(options);
		db.OperatorAiLiveStatsCacheSettings.Add(new OperatorAiLiveStatsCacheSettings
		{
			Id = 1,
			TtlMilliseconds = ttlMs,
			UpdatedAtUtc = DateTime.UtcNow,
		});
		await db.SaveChangesAsync();
	}

	private static async Task<long?> ReadTtlFromDbAsync(string dbName)
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;
		await using var db = new ApplicationDbContext(options);
		var row = await db.OperatorAiLiveStatsCacheSettings.AsNoTracking().SingleOrDefaultAsync(e => e.Id == 1);
		return row?.TtlMilliseconds;
	}

	[Fact]
	public async Task GetTtlMillisecondsAsync_caches_db_read_in_l1()
	{
		var (svc, dbName) = CreateService();
		await SeedTtlAsync(dbName, 600_000);

		var first = await svc.GetTtlMillisecondsAsync();
		await using (var db = new ApplicationDbContext(
						 new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options))
		{
			var row = await db.OperatorAiLiveStatsCacheSettings.SingleAsync();
			row.TtlMilliseconds = 120_000;
			await db.SaveChangesAsync();
		}

		var second = await svc.GetTtlMillisecondsAsync();
		first.Should().Be(600_000);
		second.Should().Be(600_000);
	}

	[Fact]
	public async Task SetTtlMillisecondsAsync_updates_l1_immediately()
	{
		var (svc, dbName) = CreateService();
		await svc.SetTtlMillisecondsAsync(180_000, "user-1");
		(await svc.GetTtlMillisecondsAsync()).Should().Be(180_000);
		(await ReadTtlFromDbAsync(dbName)).Should().Be(180_000);
	}

	[Fact]
	public async Task GetTtlMillisecondsAsync_falls_back_to_options_when_row_missing()
	{
		var (svc, _) = CreateService();
		(await svc.GetTtlMillisecondsAsync()).Should().Be(OperatorAiLiveStatsCacheConstraints.DefaultTtlMilliseconds);
	}
}
