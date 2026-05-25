namespace BeDemo.Api.Models.Requests.OperatorAi;

public sealed class CreateOperatorAiConversationRequest
{
	public string? Title { get; set; }
}

public sealed class UpdateOperatorAiConversationRequest
{
	public string? Title { get; set; }
}

public sealed class OperatorAiMessagesQuery
{
	public int Limit { get; set; } = 40;
	public int? BeforeId { get; set; }
}
