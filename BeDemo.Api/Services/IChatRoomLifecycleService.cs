namespace BeDemo.Api.Services;

public interface IChatRoomLifecycleService
{
    /// <summary>Schedule idle check ~1h from now (call after create and after each message).</summary>
    Task ScheduleIdleCheckAsync(int faceChatRoomId, CancellationToken cancellationToken = default);

    /// <summary>Worker: delete room if idle ≥1h, else reschedule check.</summary>
    Task ProcessIdleCheckAsync(int faceChatRoomId, CancellationToken cancellationToken = default);

    /// <summary>Remove room and related rows; broadcast SignalR; optionally notify creator (idle expiry).</summary>
    Task DeleteRoomCompletelyAsync(
        int faceChatRoomId,
        string reason,
        bool notifyCreatorIdleExpiry,
        CancellationToken cancellationToken = default);
}
