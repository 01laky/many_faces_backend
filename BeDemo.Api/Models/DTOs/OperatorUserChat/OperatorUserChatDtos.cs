namespace BeDemo.Api.Models.DTOs.OperatorUserChat;

public sealed record OperatorUserChatConversationDto(
    string OtherUserId,
    string OtherUserEmail,
    string OtherUserDisplayName,
    string LastMessagePreview,
    DateTime LastMessageAtUtc,
    bool LastMessageFromMe,
    int UnreadCount);

public sealed record OperatorUserChatMessageDto(
    int Id,
    string SenderId,
    string SenderName,
    string? SenderGlobalRole,
    bool IsPlatformAdministrator,
    string Content,
    DateTime SentAt,
    DateTime? ReadAt);

public sealed record OperatorUserChatHistoryPageDto(
    IReadOnlyList<OperatorUserChatMessageDto> Items,
    bool HasMore);

public sealed record OperatorUserChatThreadExistsDto(bool HasThread, int MessageCount);

public sealed class OperatorUserChatHistoryQuery
{
    public int Limit { get; set; } = 40;
    public int? BeforeId { get; set; }
}
