namespace BeDemo.Api.Services;

/// <summary>Redis-backed idle session cleanup and stale participant removal for VideoLounge.</summary>
public interface IVideoLoungeLifecycleService
{
	Task ScheduleIdleCheckAsync(int sessionId, CancellationToken cancellationToken = default);

	Task ProcessIdleCheckAsync(int sessionId, CancellationToken cancellationToken = default);

	Task ScheduleStaleParticipantCheckAsync(int sessionId, int participantId, CancellationToken cancellationToken = default);

	Task ProcessStaleParticipantCheckAsync(int sessionId, int participantId, CancellationToken cancellationToken = default);

	Task NotifyMembersSessionStartedAsync(int loungeId, int sessionId, CancellationToken cancellationToken = default);

	Task EndSessionAsync(int sessionId, string reason, CancellationToken cancellationToken = default);
}
