namespace BeDemo.Api.Services;

/// <summary>Super-admin chat room hard-delete with best-effort platform DM (not content-moderation workflow).</summary>
public interface IOperatorChatRoomManagementService
{
    /// <summary>Hard-deletes room via lifecycle service. Returns true when done (including idempotent missing room).</summary>
    Task<bool> HardDeleteRoomAsync(
        string operatorUserId,
        int roomId,
        int faceId,
        string reason,
        string userMessage,
        CancellationToken cancellationToken = default);
}
