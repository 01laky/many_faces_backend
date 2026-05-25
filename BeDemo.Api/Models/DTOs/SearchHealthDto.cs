namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Response for <c>GET /api/search/health</c>: whether search is enabled in configuration and whether the Go search-worker reports Elasticsearch reachability (via gRPC Ping).
/// </summary>
public sealed class SearchHealthDto
{
	public bool Configured { get; init; }

	public bool Reachable { get; init; }

	public string? ClusterName { get; init; }

	public string? Message { get; init; }
}
