using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Search;

/// <summary>BE-RP16 edge cases (BE-RP16-U1…U2) — reconciliation batch size and orphan cleanup.</summary>
public sealed class BeRp16SearchReconciliationEdgeTests
{
	private static (ServiceProvider Sp, FakeSearchQueryGateway Fake) Build(
		Action<SearchOptions>? configure = null,
		Action<ApplicationDbContext>? seed = null)
	{
		var fake = new FakeSearchQueryGateway();
		var opts = new SearchOptions
		{
			Enabled = true,
			WorkerGrpcUrl = "http://localhost:59996",
			ReconciliationBatchSize = 200,
		};
		configure?.Invoke(opts);

		var dbName = Guid.NewGuid().ToString("N");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
		services.AddSingleton<ISearchQueryGateway>(fake);
		services.AddSingleton<IOptions<SearchOptions>>(Options.Create(opts));
		services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
		services.AddScoped<SearchDocumentBuilder>();
		services.AddSingleton<ILogger<SearchIndexReconciliationRunner>>(_ => NullLogger<SearchIndexReconciliationRunner>.Instance);
		services.AddScoped<SearchIndexReconciliationRunner>();

		var sp = services.BuildServiceProvider();
		if (seed is not null)
		{
			using var scope = sp.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			seed(db);
			db.SaveChanges();
		}

		return (sp, fake);
	}

	/// <summary>BE-RP16-U1 — reconciliation honors configured batch size (multiple bulk calls when batch=1).</summary>
	[Fact]
	public async Task BE_RP16_U1_ReconciliationBatch_RespectsConfiguredSize()
	{
		var (sp, fake) = Build(
			o => o.ReconciliationBatchSize = 1,
			db =>
			{
				for (var i = 0; i < 3; i++)
				{
					db.Faces.Add(new Face
					{
						Index = $"rp16-{i}",
						Title = $"Face {i}",
						CreatedAt = DateTime.UtcNow,
						AllowRecensions = true,
						ChatRoomsCreate = false,
						VideoLoungesCreate = false,
					});
				}
			});

		await using (sp)
		{
			fake.BulkIndexResponse = new BulkIndexDocumentsResponse { IndexedCount = 1 };
			using var scope = sp.CreateScope();
			var runner = scope.ServiceProvider.GetRequiredService<SearchIndexReconciliationRunner>();
			await runner.RunAsync(CancellationToken.None);
			fake.BulkIndexCallCount.Should().BeGreaterThanOrEqualTo(3,
				"batch size 1 should page through faces with separate bulk calls");
		}
	}

	/// <summary>BE-RP16-U2 — ES orphan without PG row is deleted.</summary>
	[Fact]
	public async Task BE_RP16_U2_OrphanIndexEntry_Removed()
	{
		var (sp, fake) = Build();
		await using (sp)
		{
			fake.ListDocumentIdsResponse = new ListDocumentIdsResponse { EntityIds = { "424242" } };
			using var scope = sp.CreateScope();
			var runner = scope.ServiceProvider.GetRequiredService<SearchIndexReconciliationRunner>();
			await runner.RunAsync(CancellationToken.None);
			fake.DeleteDocumentCallCount.Should().BeGreaterThan(0);
		}
	}
}
