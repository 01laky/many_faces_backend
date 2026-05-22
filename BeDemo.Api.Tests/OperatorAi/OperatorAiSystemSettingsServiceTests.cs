using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class OperatorAiSystemSettingsServiceTests
{
    private static (OperatorAiSystemSettingsService Svc, string DbName) CreateService(bool defaultAiEnabled = false)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMemoryCache();
        services.AddSingleton<IHostEnvironment>(new TestingHostEnvironment());
        services.Configure<OperatorAiOptions>(o =>
        {
            o.LiveBundleCacheSettingsMemoryCacheSeconds = 30;
            o.DefaultAiEnabled = defaultAiEnabled;
        });
        var provider = services.BuildServiceProvider();
        var svc = new OperatorAiSystemSettingsService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IMemoryCache>(),
            provider.GetRequiredService<IHostEnvironment>(),
            provider.GetRequiredService<IOptions<OperatorAiOptions>>());
        return (svc, dbName);
    }

    [Fact]
    public async Task GetAsync_insert_on_first_read_defaults_to_false_in_testing()
    {
        var (svc, _) = CreateService(defaultAiEnabled: true);
        var values = await svc.GetAsync();
        values.AiEnabled.Should().BeFalse("Testing environment must ignore DefaultAiEnabled (AIS-16)");
    }

    [Fact]
    public async Task SetAsync_invalidates_l1_cache()
    {
        var (svc, dbName) = CreateService();
        _ = await svc.GetAsync();

        await svc.SetAsync(
            new OperatorAiSystemSettingsValues(true, DateTime.UtcNow, "user-1", DateTime.UtcNow, "ok"));

        var readBack = await svc.GetAsync();
        readBack.AiEnabled.Should().BeTrue();

        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options);
        var row = await db.OperatorAiSystemSettings.SingleAsync();
        row.AiEnabled.Should().BeTrue();
    }

    private sealed class TestingHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "BeDemo.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
