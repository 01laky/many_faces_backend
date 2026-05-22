namespace BeDemo.Api.Utils;

/// <summary>Stable wire values for ephemeral operator AI hub failures (3rd arg on <c>ReceiveAiMessage</c>).</summary>
public static class OperatorAiHubErrorCodes
{
    public const string InvalidLocale = "invalid_locale";
    public const string NotOperator = "not_operator";
    public const string ConversationNotFound = "conversation_not_found";
    public const string MessageTooLong = "message_too_long";
    public const string RateLimited = "rate_limited";
    public const string ModelLoading = "model_loading";
    public const string AiUnavailable = "ai_unavailable";
    public const string AiGenerationFailed = "ai_generation_failed";
    public const string AiGuardRejected = "ai_guard_rejected";
    public const string AiDisabled = "ai_disabled";
}
