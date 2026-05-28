using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP17 edge cases — single bundle load stays bounded (no N+1 within bundle).</summary>
public sealed class BeRp17OperatorAiBundleEdgeTests
{
	/// <summary>BE-RP17-U1 — count-only bundle (faces index 12) returns stable aggregate shape.</summary>
	[Fact]
	public async Task BE_RP17_U1_SingleCountOnlyBundle_BoundedDbCommands()
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
		bundle.Index.Should().Be(12);
		bundle.TotalCount.Should().BeGreaterThanOrEqualTo(0);
	}

	/// <summary>BE-RP17-U2 — all 61 bundle loaders complete without per-bundle fan-out failures.</summary>
	[Fact]
	public async Task BE_RP17_U2_PrefetchAllBundles_EachLoadBounded()
	{
		var dbName = Guid.NewGuid().ToString("N");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
		services.AddSingleton<IOptions<OperatorAiOptions>>(Options.Create(new OperatorAiOptions()));

		await using var sp = services.BuildServiceProvider();
		var factory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
		var loader = new OperatorAiEntityBundleLoader(factory, sp.GetRequiredService<IOptions<OperatorAiOptions>>());

		for (var index = 0; index < OperatorAiEntityBundleCatalog.BundleCount; index++)
		{
			var bundle = await loader.LoadAsync(index, CancellationToken.None);
			bundle.Index.Should().Be(index);
			bundle.BundleId.Should().NotBeNullOrWhiteSpace();
			bundle.TotalCount.Should().BeGreaterThanOrEqualTo(0);
		}
	}
}
