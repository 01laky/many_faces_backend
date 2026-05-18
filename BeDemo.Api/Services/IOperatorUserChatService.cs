using BeDemo.Api.Models.DTOs.OperatorUserChat;

namespace BeDemo.Api.Services;

/// <summary>Super-admin operator 1:1 user chat (per-operator threads over <c>Messages</c>).</summary>
public interface IOperatorUserChatService
{
    Task<IReadOnlyList<OperatorUserChatConversationDto>> ListConversationsAsync(
        string operatorUserId,
        CancellationToken cancellationToken = default);

    Task<OperatorUserChatHistoryPageDto?> GetHistoryAsync(
        string operatorUserId,
        string targetUserId,
        OperatorUserChatHistoryQuery query,
        CancellationToken cancellationToken = default);

    Task<OperatorUserChatThreadExistsDto> GetThreadExistsAsync(
        string operatorUserId,
        string targetUserId,
        CancellationToken cancellationToken = default);

    Task<int> MarkReadAsync(string operatorUserId, string targetUserId, CancellationToken cancellationToken = default);
}
