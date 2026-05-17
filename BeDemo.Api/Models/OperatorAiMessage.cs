namespace BeDemo.Api.Models;

/// <summary>Single turn in an <see cref="OperatorAiConversation"/> (user or assistant).</summary>
public class OperatorAiMessage
{
    public const string RoleUser = "User";
    public const string RoleAssistant = "Assistant";

    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = RoleUser;
    public string Content { get; set; } = string.Empty;

    /// <summary>Stats mode on user sends only: off | inline | live.</summary>
    public string? StatsMode { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public OperatorAiConversation? Conversation { get; set; }
}
