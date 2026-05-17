namespace BeDemo.Api.Configuration;

/// <summary>Operator AI chat (shared support inbox) — see docs/guides/admin-operator-ai-chat-threads.md.</summary>
public sealed class OperatorAiOptions
{
    public const string SectionName = "OperatorAi";

    public int MaxHistoryPairs { get; set; } = 5;
    public int MaxMessageLength { get; set; } = 16_000;
    public int MaxConversationsListPageSize { get; set; } = 50;
    public int MaxConversations { get; set; } = 1000;
    public int MessagesPageSize { get; set; } = 40;
    public int MaxNewTokens { get; set; } = 2048;
}
