namespace BeDemo.Api.Models.DTOs;

/// <summary>Knowledge reindex result returned by POST /api/operator-ai/knowledge/reindex.</summary>
public sealed class KnowledgeReindexResultDto
{
	public int IndexedCount { get; init; }
	public int FailedCount { get; init; }
	public string? EmbedModelVersion { get; init; }
}

/// <summary>Knowledge index status returned by GET /api/operator-ai/knowledge/status.</summary>
public sealed class KnowledgeIndexStatusDto
{
	public string? Alias { get; init; }
	public string? ActiveIndex { get; init; }
	public long DocCount { get; init; }
	public long ExpectedDocCount { get; init; }
	public string? EmbedModelVersion { get; init; }
	public int VectorDim { get; init; }
	public bool Ready { get; init; }
	public bool Degraded { get; init; }
	public DateTime? LastIndexedUtc { get; init; }
	public bool RebuildInProgress { get; init; }
	public string? ErrorMessage { get; init; }
}
