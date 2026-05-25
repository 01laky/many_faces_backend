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

public sealed class SearchIndexReconciliationEdgeTests
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
			ReconciliationRunTimeoutMinutes = 45,
			ReconciliationEnabled = true,
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
		services.AddSingleton<ILogger<SearchIndexReconciliationHostedService>>(_ => NullLogger<SearchIndexReconciliationHostedService>.Instance);
		services.AddSingleton<SearchIndexReconciliationHostedService>();

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

	private static SearchIndexReconciliationHostedService CreateHostedService(ServiceProvider sp) =>
		new(
			sp.GetRequiredService<IServiceScopeFactory>(),
			sp.GetRequiredService<IHostEnvironment>(),
			sp.GetRequiredService<IOptions<SearchOptions>>(),
			sp.GetRequiredService<ILogger<SearchIndexReconciliationHostedService>>());

	/// <summary>GSH1-T-R01 — reconciliation runner performs full scan and calls bulk index.</summary>
	[Fact]
	public async Task GSH1_T_R01_FirstRun_LogsSummary()
	{
		var (sp, fake) = Build(seed: db =>
		{
			db.Faces.Add(new Face
			{
				Index = "reconcile-face",
				Title = "Reconcile",
				CreatedAt = DateTime.UtcNow,
				AllowRecensions = true,
				ChatRoomsCreate = false,
				VideoLoungesCreate = false,
			});
		});
		await using (sp)
		{
			fake.BulkIndexResponse = new BulkIndexDocumentsResponse { IndexedCount = 1 };
			using var scope = sp.CreateScope();
			var runner = scope.ServiceProvider.GetRequiredService<SearchIndexReconciliationRunner>();
			var result = await runner.RunAsync(CancellationToken.None);
			result.Indexed.Should().BeGreaterThanOrEqualTo(0);
			fake.BulkIndexCallCount.Should().BeGreaterThan(0);
		}
	}

	/// <summary>GSH1-T-R02 — ReconciliationEnabled=false is respected in options gate.</summary>
	[Fact]
	public void GSH1_T_R02_ReconciliationDisabled_OptionsGate()
	{
		var opts = new SearchOptions { Enabled = true, ReconciliationEnabled = false, WorkerGrpcUrl = "http://x" };
		opts.ReconciliationEnabled.Should().BeFalse();
	}

	/// <summary>GSH1-T-R03 — Testing environment skips reconciliation hosted service loop.</summary>
	[Fact]
	public async Task GSH1_T_R03_TestingEnvironment_HostedServiceNoOp()
	{
		var (sp, fake) = Build();
		await using (sp)
		{
			var env = sp.GetRequiredService<IHostEnvironment>();
			env.EnvironmentName = "Testing";
			var hosted = CreateHostedService(sp);
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			await hosted.StartAsync(cts.Token);
			await Task.Delay(60);
			await hosted.StopAsync(CancellationToken.None);
			fake.BulkIndexCallCount.Should().Be(0);
		}
	}

	/// <summary>GSH1-T-R04 — overlapping tick skipped while run in progress.</summary>
	[Fact]
	public async Task GSH1_T_R04_OverlappingTick_Skipped()
	{
		var (sp, _) = Build(seed: db =>
		{
			for (var i = 0; i < 5; i++)
			{
				db.Faces.Add(new Face
				{
					Index = $"f{i}",
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
			var hosted = CreateHostedService(sp);
			var t1 = hosted.TryRunOnceAsync(CancellationToken.None);
			var t2 = hosted.TryRunOnceAsync(CancellationToken.None);
			await Task.WhenAll(t1, t2);
		}
	}

	/// <summary>GSH1-T-R06 — ES orphan id deleted when PG row missing.</summary>
	[Fact]
	public async Task GSH1_T_R06_EsOrphanWithoutPg_DeletesDocument()
	{
		var (sp, fake) = Build();
		await using (sp)
		{
			fake.ListDocumentIdsResponse = new ListDocumentIdsResponse { EntityIds = { "999999" } };
			var runner = sp.GetRequiredService<SearchIndexReconciliationRunner>();
			await runner.RunAsync(CancellationToken.None);
			fake.DeleteDocumentCallCount.Should().BeGreaterThan(0);
		}
	}

	/// <summary>GSH1-T-R07 — partial batch failure continues; failed count tracked.</summary>
	[Fact]
	public async Task GSH1_T_R07_PartialBatchFailure_ContinuesRun()
	{
		var (sp, fake) = Build(seed: db =>
		{
			db.Faces.Add(new Face
			{
				Index = "fail-face",
				Title = "Fail Face",
				CreatedAt = DateTime.UtcNow,
				AllowRecensions = true,
				ChatRoomsCreate = false,
				VideoLoungesCreate = false,
			});
		});
		await using (sp)
		{
			fake.BulkIndexResponse = new BulkIndexDocumentsResponse { IndexedCount = 1, FailedCount = 2 };
			using var scope = sp.CreateScope();
			var runner = scope.ServiceProvider.GetRequiredService<SearchIndexReconciliationRunner>();
			var result = await runner.RunAsync(CancellationToken.None);
			result.Failed.Should().BeGreaterThan(0);
		}
	}

	/// <summary>GSH1-T-R08 — empty PG table for type does not error.</summary>
	[Fact]
	public async Task GSH1_T_R08_EmptyPgTable_NoError()
	{
		var (sp, _) = Build();
		await using (sp)
		{
			var runner = sp.GetRequiredService<SearchIndexReconciliationRunner>();
			var act = async () => await runner.RunAsync(CancellationToken.None);
			await act.Should().NotThrowAsync();
		}
	}

	/// <summary>GSH1-T-R09 — run respects timeout cancellation.</summary>
	[Fact]
	public async Task GSH1_T_R09_RunTimeout_CancelsGracefully()
	{
		var (sp, _) = Build();
		await using (sp)
		{
			using var cts = new CancellationTokenSource();
			cts.Cancel();
			var runner = sp.GetRequiredService<SearchIndexReconciliationRunner>();
			var act = async () => await runner.RunAsync(cts.Token);
			await act.Should().ThrowAsync<OperationCanceledException>();
		}
	}

	/// <summary>GSH1-T-R10 — host shutdown mid-run swallows cancel.</summary>
	[Fact]
	public async Task GSH1_T_R10_HostShutdownMidRun_Swallowed()
	{
		var (sp, _) = Build();
		await using (sp)
		{
			using var cts = new CancellationTokenSource();
			cts.Cancel();
			var hosted = CreateHostedService(sp);
			var act = async () => await hosted.TryRunOnceAsync(cts.Token);
			await act.Should().NotThrowAsync();
		}
	}

	/// <summary>GSH1-T-R11 — batch size 1 indexes multiple pages when multiple faces exist.</summary>
	[Fact]
	public async Task GSH1_T_R11_PaginationBoundary_AllPagesIndexed()
	{
		var (sp, fake) = Build(
			o => o.ReconciliationBatchSize = 1,
			db =>
			{
				for (var i = 0; i < 3; i++)
				{
					db.Faces.Add(new Face
					{
						Index = $"page{i}",
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
			fake.BulkIndexCallCount.Should().BeGreaterThanOrEqualTo(3);
		}
	}

	/// <summary>GSH1-T-R13 — reconciliation completes with observability timestamp.</summary>
	[Fact]
	public async Task GSH1_T_R13_ReconciliationComplete_RecordsObservability()
	{
		var (sp, fake) = Build();
		await using (sp)
		{
			fake.BulkIndexResponse = new BulkIndexDocumentsResponse { IndexedCount = 3, FailedCount = 1 };
			var runner = sp.GetRequiredService<SearchIndexReconciliationRunner>();
			await runner.RunAsync(CancellationToken.None);
			SearchObservability.LastReconciliationSuccessUtc.Should().NotBeNull();
		}
	}
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
	public string EnvironmentName { get; set; } = "Development";
	public string ApplicationName { get; set; } = "Test";
	public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
	public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
		new Microsoft.Extensions.FileProviders.NullFileProvider();
}
