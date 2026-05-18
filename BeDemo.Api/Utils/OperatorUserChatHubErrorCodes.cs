namespace BeDemo.Api.Utils;

/// <summary>Stable wire values for super-admin platform chat hub failures (<c>ReceivePlatformChatError</c>).</summary>
public static class OperatorUserChatHubErrorCodes
{
    public const string NotSuperAdmin = "not_super_admin";
    public const string TargetNotFound = "target_not_found";
    public const string CannotMessageSuperAdmin = "cannot_message_super_admin";
    public const string CannotMessageSelf = "cannot_message_self";
    public const string MessageTooLong = "message_too_long";
    public const string RateLimited = "rate_limited";
    public const string EmptyContent = "empty_content";
    public const string NoPlatformThread = "no_platform_thread";
}
