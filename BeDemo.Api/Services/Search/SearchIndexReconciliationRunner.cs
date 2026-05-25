using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>Full PG scan + ES orphan cleanup for scheduled reconciliation (§6.2).</summary>
public sealed class SearchIndexReconciliationRunner
{
    private readonly ISearchQueryGateway _gateway;
    private readonly SearchDocumentBuilder _builder;
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchIndexReconciliationRunner> _logger;

    public SearchIndexReconciliationRunner(
        ISearchQueryGateway gateway,
        SearchDocumentBuilder builder,
        IOptions<SearchOptions> options,
        ILogger<SearchIndexReconciliationRunner> logger)
    {
        _gateway = gateway;
        _builder = builder;
        _options = options;
        _logger = logger;
    }

    public async Task<(int Indexed, int Deleted, int Failed)> RunAsync(CancellationToken cancellationToken)
    {
        var correlationId = SearchWorkerGrpcGateway.NewCorrelationId();
        var started = DateTime.UtcNow;
        var indexed = 0;
        var deleted = 0;
        var failed = 0;
        var batchSize = Math.Max(1, _options.Value.ReconciliationBatchSize);

        if (!_options.Value.IsEnabled || !_gateway.IsAvailable)
            return (0, 0, 0);

        foreach (var documentType in SearchDocumentTypes.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skip = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = await _builder.ReadIndexableBatchAsync(documentType, skip, batchSize, cancellationToken);
                if (batch.Count == 0)
                    break;

                try
                {
                    var response = await _gateway.BulkIndexDocumentsAsync(new BulkIndexDocumentsRequest
                    {
                        CorrelationId = correlationId,
                        Documents = { batch },
                    }, cancellationToken);

                    if (response is null)
                    {
                        failed += batch.Count;
                    }
                    else
                    {
                        indexed += response.IndexedCount;
                        failed += response.FailedCount;
                    }
                }
                catch (Exception ex)
                {
                    failed += batch.Count;
                    _logger.LogWarning(ex, "Reconciliation bulk index failed for {DocumentType} skip={Skip}", documentType, skip);
                }

                skip += batchSize;
                if (batch.Count < batchSize)
                    break;
            }

            try
            {
                var pgIds = await _builder.GetIndexableEntityIdsAsync(documentType, cancellationToken);
                var esIds = await ListAllEsIdsAsync(documentType, correlationId, cancellationToken);
                foreach (var orphanId in esIds.Where(id => !pgIds.Contains(id)))
                {
                    try
                    {
                        var del = await _gateway.DeleteDocumentAsync(new DeleteDocumentRequest
                        {
                            DocumentType = documentType,
                            EntityId = orphanId,
                            CorrelationId = correlationId,
                        }, cancellationToken);
                        if (del?.Success == true)
                            deleted++;
                        else
                            failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex, "Orphan delete failed {DocumentType}/{EntityId}", documentType, orphanId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Orphan cleanup failed for {DocumentType}", documentType);
                failed++;
            }
        }

        var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        SearchObservability.LogReconciliationComplete(_logger, indexed, deleted, failed, durationMs, correlationId);
        return (indexed, deleted, failed);
    }

    private async Task<HashSet<string>> ListAllEsIdsAsync(
        string documentType,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cursor = string.Empty;
        while (true)
        {
            var response = await _gateway.ListDocumentIdsAsync(new ListDocumentIdsRequest
            {
                DocumentType = documentType,
                Cursor = cursor,
                PageSize = 500,
                CorrelationId = correlationId,
            }, cancellationToken);

            if (response is null || response.EntityIds.Count == 0)
                break;

            foreach (var id in response.EntityIds)
                ids.Add(id);

            if (string.IsNullOrWhiteSpace(response.NextCursor))
                break;

            cursor = response.NextCursor;
        }

        return ids;
    }
}
