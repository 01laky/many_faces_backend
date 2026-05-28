using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP10 edge cases (BE-RP10-U1…U2). DbContext pool waived — host uses AddDbContext + interceptors.</summary>
public sealed class BeRp10DbContextPoolEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp10DbContextPoolEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP10-U1 — integration host starts (pool not enabled; AddDbContext + interceptors remain safe).</summary>
	[Fact]
	public void BE_RP10_U1_TestHost_StartsWithDbContextAndInterceptors()
	{
		using var client = _factory.CreateClient();
		client.BaseAddress.Should().NotBeNull();
		_factory.Services.GetService<SearchOutboxSaveChangesInterceptor>().Should().NotBeNull();
		_factory.Services.GetService<ApplicationDbContext>().Should().NotBeNull();
	}

	/// <summary>BE-RP10-U2 — SaveChanges still stages search outbox when search is enabled.</summary>
	[Fact]
	public async Task BE_RP10_U2_SaveChanges_StagesSearchOutboxWhenSearchEnabled()
	{
		var dbName = Guid.NewGuid().ToString("N");
		var searchOptions = new SearchOptions
		{
			Enabled = true,
			WorkerGrpcUrl = "http://localhost:59996",
		};

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<FixedOptionsMonitor<SearchOptions>>(new FixedOptionsMonitor<SearchOptions>(searchOptions));
		services.AddSingleton<IOptionsMonitor<SearchOptions>>(sp => sp.GetRequiredService<FixedOptionsMonitor<SearchOptions>>());
		services.AddSingleton<SearchOutboxSaveChangesInterceptor>();
		services.AddDbContext<ApplicationDbContext>((sp, options) =>
		{
			options.UseInMemoryDatabase(dbName);
			options.AddInterceptors(sp.GetRequiredService<SearchOutboxSaveChangesInterceptor>());
		});

		await using var sp = services.BuildServiceProvider();
		await using var scope = sp.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var face = new Face
		{
			Index = "outbox-pool-edge",
			Title = "Outbox",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = false,
			VideoLoungesCreate = false,
		};
		db.Faces.Add(face);
		await db.SaveChangesAsync();

		(await db.SearchOutboxEntries.CountAsync()).Should().BeGreaterThan(0);
	}
}
