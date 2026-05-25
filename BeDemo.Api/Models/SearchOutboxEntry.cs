namespace BeDemo.Api.Models;

/// <summary>Pending search index/delete operation drained by <see cref="Services.Search.SearchOutboxProcessorHostedService"/>.</summary>
public class SearchOutboxEntry
{
    public long Id { get; set; }

    /// <summary>Elasticsearch document type (see <see cref="Services.Search.SearchDocumentTypes"/>).</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Primary entity identifier as stored in the search index.</summary>
    public string EntityId { get; set; } = string.Empty;

    public SearchOutboxOperation Operation { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null while pending; set when the worker RPC succeeded.</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}

public enum SearchOutboxOperation
{
    Index = 0,
    Delete = 1,
}
