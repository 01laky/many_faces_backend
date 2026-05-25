namespace BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>Wire DTO for GET/PUT <c>/api/operator-ai/system-settings</c>.</summary>
public sealed class OperatorAiSystemSettingsDto
{
	public bool AiEnabled { get; set; }

	public DateTime UpdatedAtUtc { get; set; }

	public string? UpdatedByUserId { get; set; }

	public DateTime? LastEnabledAtUtc { get; set; }

	public string? LastEnableHealthStatus { get; set; }
}
