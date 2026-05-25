namespace BeDemo.Api.Services.Search;

/// <summary>Enqueues index/delete operations into <see cref="Models.SearchOutboxEntry"/>.</summary>
public interface ISearchOutboxService
{
    Task EnqueueIndexAsync(string documentType, string entityId, CancellationToken cancellationToken = default);

    Task EnqueueDeleteAsync(string documentType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Upserts a pending row without calling SaveChanges (participates in caller transaction).</summary>
    void StageIndex(string documentType, string entityId);

    void StageDelete(string documentType, string entityId);
}
