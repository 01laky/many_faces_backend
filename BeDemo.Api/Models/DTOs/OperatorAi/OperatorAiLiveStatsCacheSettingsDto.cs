namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiLiveStatsCacheSettingsDto
{
	public required long TtlMilliseconds { get; init; }
	public required long DefaultTtlMilliseconds { get; init; }
	public required long MinTtlMilliseconds { get; init; }
	public required long MaxTtlMilliseconds { get; init; }
}
