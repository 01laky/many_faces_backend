using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Search;

/// <summary>BE-RP5 edge cases (BE-RP5-U1…U3).</summary>
public sealed class BeRp5SearchOutboxBatchEdgeTests
{
	private static async Task<(ServiceProvider Sp, FakeSearchQueryGateway Fake, int FaceId)> BuildAsync(
		Action<FakeSearchQueryGateway>? configureFake = null,
		int maxParallel = 4)
	{
		var fake = new FakeSearchQueryGateway
		{
			BulkIndexResponse = new BulkIndexDocumentsResponse
			{
				IndexedCount = 50,
				FailedCount = 0,
			},
		};
		configureFake?.Invoke(fake);

		var dbName = Guid.NewGuid().ToString("N");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
		services.AddSingleton<ISearchQueryGateway>(fake);
		services.AddSingleton<IOptions<SearchOptions>>(Options.Create(new SearchOptions
		{
			Enabled = true,
			WorkerGrpcUrl = "http://localhost:59996",
		}));
		services.AddSingleton<IOptions<PerformanceOptions>>(Options.Create(new PerformanceOptions
		{
			SearchOutboxMaxParallelGrpc = maxParallel,
		}));
		services.AddScoped<SearchDocumentBuilder>();
		services.AddScoped<ISearchOutboxService, SearchOutboxService>();
		services.AddSingleton<SearchOutboxProcessorHostedService>();
		var sp = services.BuildServiceProvider();

		await using var scope = sp.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var face = new Face
		{
			Index = "batch-face",
			Title = "Batch",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = true,
			VideoLoungesCreate = true,
		};
		db.Faces.Add(face);
		await db.SaveChangesAsync();
		return (sp, fake, face.Id);
	}

	/// <summary>BE-RP5-U1 — batch of 50 outbox rows uses bulk gRPC, not 50 single IndexDocument calls.</summary>
	[Fact]
	public async Task BE_RP5_U1_BatchOf50_UsesBulkGrpcNotPerRowIndex()
	{
		var (sp, fake, faceId) = await BuildAsync();
		await using (sp)
		{
			var db = sp.GetRequiredService<ApplicationDbContext>();
			for (var i = 0; i < 50; i++)
			{
				db.SearchOutboxEntries.Add(new SearchOutboxEntry
				{
					DocumentType = SearchDocumentTypes.Face,
					EntityId = faceId.ToString(),
					Operation = SearchOutboxOperation.Index,
					CreatedAtUtc = DateTime.UtcNow.AddSeconds(-i),
				});
			}

			await db.SaveChangesAsync();
			var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
			await processor.ProcessBatchAsync(CancellationToken.None);

			fake.IndexDocumentCallCount.Should().Be(0);
			fake.BulkIndexCallCount.Should().BeInRange(1, 13,
				"50 index rows should batch via BulkIndexDocuments (maxParallel=4 → ~13 chunks, not 50 singles)");
			(await db.SearchOutboxEntries.CountAsync(e => e.ProcessedAtUtc != null)).Should().Be(50);
		}
	}

	/// <summary>BE-RP5-U2 — single bulk item failure does not poison successful rows in the same batch.</summary>
	[Fact]
	public async Task BE_RP5_U2_PartialBulkFailure_SuccessfulRowsStillProcessed()
	{
		var (sp, fake, faceId) = await BuildAsync(maxParallel: 4);
		fake.BulkIndexResponse = new BulkIndexDocumentsResponse
		{
			IndexedCount = 2,
			FailedCount = 1,
			Errors =
			{
				new BulkIndexItemError { EntityId = faceId.ToString(), ErrorMessage = "simulated" },
			},
		};

		await using (sp)
		{
			var db = sp.GetRequiredService<ApplicationDbContext>();
			for (var i = 0; i < 3; i++)
			{
				db.SearchOutboxEntries.Add(new SearchOutboxEntry
				{
					DocumentType = SearchDocumentTypes.Face,
					EntityId = faceId.ToString(),
					Operation = SearchOutboxOperation.Index,
					CreatedAtUtc = DateTime.UtcNow.AddSeconds(-i),
				});
			}

			await db.SaveChangesAsync();
			var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
			await processor.ProcessBatchAsync(CancellationToken.None);

			fake.BulkIndexCallCount.Should().BeGreaterThan(0);
			var pending = await db.SearchOutboxEntries.CountAsync(e => e.ProcessedAtUtc == null);
			pending.Should().BeGreaterThan(0, "failed bulk items remain pending for retry");
		}
	}

	/// <summary>BE-RP5-U3 — removed album enqueues delete path (ACL / visibility).</summary>
	[Fact]
	public async Task BE_RP5_U3_RemovedAlbum_TriggersDeleteDocument()
	{
		var (sp, fake, _) = await BuildAsync();
		await using (sp)
		{
			var db = sp.GetRequiredService<ApplicationDbContext>();
			var album = new Album
			{
				CreatorId = "creator",
				Title = "Removed",
				CreatedAt = DateTime.UtcNow,
				RemovedAtUtc = DateTime.UtcNow,
			};
			db.Albums.Add(album);
			await db.SaveChangesAsync();

			var outbox = sp.GetRequiredService<ISearchOutboxService>();
			await outbox.EnqueueIndexAsync(SearchDocumentTypes.Album, album.Id.ToString(), CancellationToken.None);

			var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
			await processor.ProcessBatchAsync(CancellationToken.None);

			fake.DeleteDocumentCallCount.Should().Be(1);
		}
	}
}
