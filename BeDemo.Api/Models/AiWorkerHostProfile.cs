namespace BeDemo.Api.Models;

/// <summary>
/// Latest host hardware/OS snapshot reported by the Python AI worker (GetHostProfile gRPC).
/// </summary>
public class AiWorkerHostProfile
{
	public int Id { get; set; }

	/// <summary>Stable worker id from Python (sha256:…).</summary>
	public string WorkerInstanceId { get; set; } = string.Empty;

	public DateTime CollectedAtUtc { get; set; }

	/// <summary>Configured gRPC address used when this profile was refreshed.</summary>
	public string GrpcAddressLastSeen { get; set; } = string.Empty;

	/// <summary>Full JSON snapshot from the worker.</summary>
	public string ProfileJson { get; set; } = "{}";

	public string? Hostname { get; set; }

	public string? OsDisplayName { get; set; }

	public int? CpuLogicalCores { get; set; }

	public string? GpuPrimaryName { get; set; }

	public long? GpuVramBytes { get; set; }

	public long? RamTotalBytes { get; set; }

	public DateTime UpdatedAtUtc { get; set; }
}
