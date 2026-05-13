using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Readiness-style probe for the optional search stack: reaches the Go search-worker in
/// many_faces_elastic over gRPC and interprets its Ping RPC results.
/// The API never opens Elasticsearch HTTP for this path; Elasticsearch connectivity is evaluated inside the worker only.
/// </summary>
public interface ISearchWorkerProbe
{
    /// <summary>
    /// Returns whether search is configured (<see cref="SearchOptions"/>) and whether the worker reports a healthy Elasticsearch projection.
    /// </summary>
    Task<SearchHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
}
