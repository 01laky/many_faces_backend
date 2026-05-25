namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiMessageAppendedEventDto
{
	public int ConversationId { get; set; }
	public OperatorAiMessageDto UserMessage { get; set; } = null!;
	public OperatorAiMessageDto AssistantMessage { get; set; } = null!;
	public OperatorAiConversationListItemDto Conversation { get; set; } = null!;
}

public sealed class OperatorAiConversationDeletedEventDto
{
	public int ConversationId { get; set; }
}
