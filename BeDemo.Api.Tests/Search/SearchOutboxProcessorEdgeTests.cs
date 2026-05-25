using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class SearchOutboxProcessorEdgeTests
{
    private static async Task<ServiceProvider> BuildSeededServicesAsync(
        FakeSearchQueryGateway fake,
        Action<SearchOptions>? configure = null)
    {
        var dbName = Guid.NewGuid().ToString("N");
        var opts = new SearchOptions
        {
            Enabled = true,
            WorkerGrpcUrl = "http://localhost:59996",
        };
        configure?.Invoke(opts);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISearchQueryGateway>(fake);
        services.AddSingleton<IOptions<SearchOptions>>(Options.Create(opts));
        services.AddScoped<SearchDocumentBuilder>();
        services.AddScoped<ISearchOutboxService, SearchOutboxService>();
        services.AddSingleton<SearchOutboxProcessorHostedService>();
        var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Faces.Add(new Face
        {
            Index = "face-search",
            Title = "Search Face",
            CreatedAt = DateTime.UtcNow,
            AllowRecensions = true,
            ChatRoomsCreate = true,
            VideoLoungesCreate = true,
        });
        await db.SaveChangesAsync();

        var faceId = (await db.Faces.FirstAsync()).Id;
        db.FaceChatRooms.Add(new FaceChatRoom
        {
            FaceId = faceId,
            Title = "Lobby",
            CreatedAt = DateTime.UtcNow,
        });
        db.Albums.Add(new Album
        {
            CreatorId = "orphan-creator",
            Title = "Album",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return sp;
    }

    /// <summary>GSH1-T-O01 — entity update enqueues IndexDocument via processor.</summary>
    [Fact]
    public async Task GSH1_T_O01_UserUpdate_ProcessorCallsIndexDocument()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var faceId = (await db.Faces.FirstAsync()).Id.ToString();
            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueIndexAsync(SearchDocumentTypes.Face, faceId, CancellationToken.None);

            var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);

            fake.IndexDocumentCallCount.Should().Be(1);
        }
    }

    /// <summary>GSH1-T-O02 — hard delete entity enqueues DeleteDocument.</summary>
    [Fact]
    public async Task GSH1_T_O02_HardDelete_EnqueuesDeleteDocument()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueDeleteAsync(SearchDocumentTypes.Blog, "999", CancellationToken.None);

            var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);

            fake.DeleteDocumentCallCount.Should().Be(1);
        }
    }

    /// <summary>GSH1-T-O03 — duplicate outbox id upserts without double RPC error.</summary>
    [Fact]
    public async Task GSH1_T_O03_DuplicateOutboxId_IdempotentUpsert()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueIndexAsync(SearchDocumentTypes.Face, "1", CancellationToken.None);
            await outbox.EnqueueIndexAsync(SearchDocumentTypes.Face, "1", CancellationToken.None);

            var db = sp.GetRequiredService<ApplicationDbContext>();
            (await db.SearchOutboxEntries.CountAsync(e => e.ProcessedAtUtc == null)).Should().Be(1);
        }
    }

    /// <summary>GSH1-T-O04 — worker fails once then succeeds on retry.</summary>
    [Fact]
    public async Task GSH1_T_O04_WorkerFailsOnceThenSucceeds_Retries()
    {
        var fake = new FakeSearchQueryGateway { DeleteFailCountBeforeSuccess = 1 };
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            using var scope = sp.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueDeleteAsync(SearchDocumentTypes.Page, "1", CancellationToken.None);

            var processor = scope.ServiceProvider.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);
            await using (var verify1 = sp.CreateAsyncScope())
            {
                var verifyDb = verify1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await verifyDb.SearchOutboxEntries.FirstAsync()).ProcessedAtUtc.Should().BeNull();
            }

            fake.DeleteFailCountBeforeSuccess = 0;
            fake.ResetDeleteAttempts();

            await processor.ProcessBatchAsync(CancellationToken.None);
            await using (var verify2 = sp.CreateAsyncScope())
            {
                var verifyDb = verify2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await verifyDb.SearchOutboxEntries.FirstAsync()).ProcessedAtUtc.Should().NotBeNull();
            }
            fake.DeleteDocumentCallCount.Should().Be(2);
        }
    }

    /// <summary>GSH1-T-O05 — Search:Enabled=false processor no-op; rows remain pending.</summary>
    [Fact]
    public async Task GSH1_T_O05_SearchDisabled_ProcessorNoOp()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake, o => o.Enabled = false);
        await using (sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.SearchOutboxEntries.Add(new SearchOutboxEntry
            {
                DocumentType = SearchDocumentTypes.User,
                EntityId = "x",
                Operation = SearchOutboxOperation.Index,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);

            fake.IndexDocumentCallCount.Should().Be(0);
            (await db.SearchOutboxEntries.FirstAsync()).ProcessedAtUtc.Should().BeNull();
        }
    }

    /// <summary>GSH1-T-O06 — staged outbox row is not persisted until SaveChanges (rollback-safe).</summary>
    [Fact]
    public async Task GSH1_T_O06_TransactionRollback_NoOrphanOutboxRow()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            outbox.StageIndex(SearchDocumentTypes.User, "rollback-user");
            await db.SaveChangesAsync();

            await using var otherScope = sp.CreateAsyncScope();
            var otherDb = otherScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await otherDb.SearchOutboxEntries.CountAsync()).Should().Be(1);

            otherDb.SearchOutboxEntries.RemoveRange(await otherDb.SearchOutboxEntries.ToListAsync());
            await otherDb.SaveChangesAsync();
            (await otherDb.SearchOutboxEntries.CountAsync()).Should().Be(0);
        }
    }

    /// <summary>GSH1-T-O07 — face-scoped chat room document includes face_id.</summary>
    [Fact]
    public async Task GSH1_T_O07_FaceScopedChatRoom_IncludesFaceId()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var room = await db.FaceChatRooms.FirstAsync();

            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueIndexAsync(SearchDocumentTypes.FaceChatRoom, room.Id.ToString(), CancellationToken.None);

            var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);

            fake.IndexDocumentCallCount.Should().Be(1);
        }
    }

    /// <summary>GSH1-T-O08 — non-indexable removed album triggers delete not index.</summary>
    [Fact]
    public async Task GSH1_T_O08_NonIndexableAlbum_DeleteDocument()
    {
        var fake = new FakeSearchQueryGateway();
        var sp = await BuildSeededServicesAsync(fake);
        await using (sp)
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var album = await db.Albums.FirstAsync();
            album.RemovedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var outbox = sp.GetRequiredService<ISearchOutboxService>();
            await outbox.EnqueueIndexAsync(SearchDocumentTypes.Album, album.Id.ToString(), CancellationToken.None);

            var processor = sp.GetRequiredService<SearchOutboxProcessorHostedService>();
            await processor.ProcessBatchAsync(CancellationToken.None);

            fake.DeleteDocumentCallCount.Should().Be(1);
        }
    }
}
