namespace BeDemo.Api.Services;

/// <summary>
/// Persists platform direct messages (super-admin ↔ end user) with notifications and SignalR delivery.
/// Used by REST <c>platform-messages</c> and <c>MessengerHub.SendPlatformDirectMessage</c>.
/// </summary>
public interface IPlatformDirectMessageService
{
    /// <summary>
    /// Sends a platform DM when rules allow (super-admin → user, or user → super-admin on existing platform thread).
    /// </summary>
    /// <returns>Hub error code when failed; null on success with <paramref name="messageId"/> set.</returns>
    Task<(string? HubErrorCode, int? MessageId)> SendAsync(
        string senderId,
        string receiverId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>Whether any platform thread exists between the two users (for UI exists endpoint).</summary>
    Task<bool> ThreadExistsAsync(string operatorUserId, string targetUserId, CancellationToken cancellationToken = default);
}
