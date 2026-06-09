namespace BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>
/// 7B-perf O4 — one streamed token delta pushed to the operator group during generation (SignalR event
/// <c>OperatorAiMessageDelta</c>). The admin appends each <see cref="Delta"/> to a transient assistant bubble for
/// the matching <see cref="ConversationId"/>, then reconciles to the persisted message on <c>OperatorAiMessageAppended</c>.
/// </summary>
public sealed class OperatorAiMessageDeltaEventDto
{
	public int ConversationId { get; set; }

	public string Delta { get; set; } = string.Empty;
}
