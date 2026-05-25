namespace BeDemo.Api.Models;

/// <summary>
/// Singleton row (Id=1) tracking the last backend refresh attempt against the AI worker.
/// </summary>
public class AiWorkerHostRefreshMeta
{
	public int Id { get; set; }

	public DateTime? LastRefreshAttemptUtc { get; set; }

	public bool LastRefreshSucceeded { get; set; }

	public string? LastRefreshError { get; set; }

	public string? GrpcAddressConfigured { get; set; }
}
