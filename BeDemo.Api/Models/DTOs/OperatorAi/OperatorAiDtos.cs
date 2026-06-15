namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiConversationListItemDto
{
	public int Id { get; set; }
	public string? Title { get; set; }
	public string CreatedByUserId { get; set; } = string.Empty;
	public string? CreatedByDisplayName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public sealed class OperatorAiMessageDto
{
	public int Id { get; set; }
	public string Role { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public string? StatsMode { get; set; }
	public string? CreatedByUserId { get; set; }
	public string? AuthorEmail { get; set; }
	public string? ResponseLocale { get; set; }

	/// <summary>Assistant rows only: server-measured request duration in milliseconds; null for user/legacy rows.</summary>
	public long? DurationMs { get; set; }

	public DateTime CreatedAt { get; set; }
}

public sealed class OperatorAiMessagesPageDto
{
	public IReadOnlyList<OperatorAiMessageDto> Items { get; set; } = Array.Empty<OperatorAiMessageDto>();
	public bool HasMore { get; set; }
	public int? OldestId { get; set; }
}
