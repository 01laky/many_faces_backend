using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Search;

/// <summary>BE-RP32 edge cases (BE-RP32-U1…U3) — gRPC deadlines and outbox concurrency cap.</summary>
public sealed class BeRp32GrpcDeadlineEdgeTests
{
	/// <summary>BE-RP32-U1 — shared deadline helper and hung gateway honor cancellation.</summary>
	[Fact]
	public void BE_RP32_U1_SearchCallOptions_DeadlineWithinConfiguredSeconds()
	{
		var options = Options.Create(new PerformanceOptions { SearchGrpcDeadlineSeconds = 7 });
		var callOptions = GrpcCallDefaults.SearchCallOptions(options);
		callOptions.Deadline.Should().NotBeNull();
		var remaining = callOptions.Deadline!.Value - DateTime.UtcNow;
		remaining.TotalSeconds.Should().BeInRange(1, 8);
	}

	[Fact]
	public async Task BE_RP32_U1b_SlowGateway_CancelsWhenDeadlineTokenFires()
	{
		var fake = new FakeSearchQueryGateway { BulkIndexDelayMs = 30_000 };
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		var act = async () => await fake.BulkIndexDocumentsAsync(new ManyFaces.Search.V1.BulkIndexDocumentsRequest(), cts.Token);
		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	/// <summary>BE-RP32-U2 — outbox bulk gRPC respects SearchOutboxMaxParallelGrpc cap.</summary>
	[Fact]
	public async Task BE_RP32_U2_OutboxBatch_ConcurrencyCapRespected()
	{
		const int maxParallel = 2;
		var fake = new FakeSearchQueryGateway
		{
			BulkIndexDelayMs = 80,
			BulkIndexResponse = new ManyFaces.Search.V1.BulkIndexDocumentsResponse { IndexedCount = 10 },
		};

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

		await using var sp = services.BuildServiceProvider();
		var db = sp.GetRequiredService<ApplicationDbContext>();
		var face = new Face
		{
			Index = "grpc-cap",
			Title = "Cap",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = true,
			VideoLoungesCreate = true,
		};
		db.Faces.Add(face);
		await db.SaveChangesAsync();

		for (var i = 0; i < 24; i++)
		{
			db.SearchOutboxEntries.Add(new SearchOutboxEntry
			{
				DocumentType = SearchDocumentTypes.Face,
				EntityId = face.Id.ToString(),
				Operation = SearchOutboxOperation.Index,
				CreatedAtUtc = DateTime.UtcNow.AddSeconds(-i),
			});
		}

		await db.SaveChangesAsync();
		var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
		await processor.ProcessBatchAsync(CancellationToken.None);

		fake.MaxObservedBulkConcurrency.Should().BeLessThanOrEqualTo(maxParallel);
		fake.BulkIndexCallCount.Should().BeGreaterThan(0);
	}

	/// <summary>BE-RP32-U3 — search fake gateway smoke (Search/* suite dependency).</summary>
	[Fact]
	public async Task BE_RP32_U3_SearchFakeGateway_RemainsHealthy()
	{
		var fake = new FakeSearchQueryGateway();
		var response = await fake.AutocompleteAsync(new ManyFaces.Search.V1.AutocompleteRequest());
		response.Should().BeNull();
		fake.IsAvailable.Should().BeTrue();
	}
}
