using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>Idempotent outbox enqueue for incremental search indexing (§6.1).</summary>
public sealed class SearchOutboxService : ISearchOutboxService
{
    private readonly ApplicationDbContext _db;
    private readonly IOptions<SearchOptions> _options;

    public SearchOutboxService(ApplicationDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options;
    }

    /// <inheritdoc />
    public async Task EnqueueIndexAsync(string documentType, string entityId, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.IsEnabled)
            return;

        StageIndex(documentType, entityId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnqueueDeleteAsync(string documentType, string entityId, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.IsEnabled)
            return;

        StageDelete(documentType, entityId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void StageIndex(string documentType, string entityId) =>
        Upsert(documentType, entityId, SearchOutboxOperation.Index);

    /// <inheritdoc />
    public void StageDelete(string documentType, string entityId) =>
        Upsert(documentType, entityId, SearchOutboxOperation.Delete);

    private void Upsert(string documentType, string entityId, SearchOutboxOperation operation)
    {
        if (!_options.Value.IsEnabled)
            return;

        var existing = _db.SearchOutboxEntries
            .FirstOrDefault(e => e.DocumentType == documentType && e.EntityId == entityId && e.ProcessedAtUtc == null);

        if (existing is not null)
        {
            existing.Operation = operation;
            existing.CreatedAtUtc = DateTime.UtcNow;
            existing.AttemptCount = 0;
            existing.LastError = null;
            return;
        }

        _db.SearchOutboxEntries.Add(new SearchOutboxEntry
        {
            DocumentType = documentType,
            EntityId = entityId,
            Operation = operation,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
