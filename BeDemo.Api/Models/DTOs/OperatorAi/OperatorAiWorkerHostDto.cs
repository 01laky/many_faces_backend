using System.Text.Json;

namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiWorkerHostDto
{
	public bool Reachable { get; set; }

	public DateTime? LastRefreshAttemptUtc { get; set; }

	public string? LastRefreshError { get; set; }

	public string? GrpcAddressConfigured { get; set; }

	/// <summary>Parsed host profile JSON or null when never collected.</summary>
	public JsonElement? Profile { get; set; }
}
