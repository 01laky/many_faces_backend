namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiModelStatusDto
{
	public bool Ready { get; set; }
	public bool Loading { get; set; }
	public bool Unavailable { get; set; }
	public string? ModelName { get; set; }
}
