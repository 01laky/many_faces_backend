using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <inheritdoc />
public sealed class VideoLoungeLifecycleService : IVideoLoungeLifecycleService
{
	public const string JobIdleCheck = "video-lounge.idle-check";
	public const string JobStaleParticipant = "video-lounge.participant-stale";

	private readonly ApplicationDbContext _context;
	private readonly IRedisJobQueue _jobQueue;
	private readonly IHubContext<VideoLoungeHub> _loungeHub;
	private readonly IHubContext<MessengerHub> _messengerHub;
	private readonly ILogger<VideoLoungeLifecycleService> _logger;

	public VideoLoungeLifecycleService(
		ApplicationDbContext context,
		IRedisJobQueue jobQueue,
		IHubContext<VideoLoungeHub> loungeHub,
		IHubContext<MessengerHub> messengerHub,
		ILogger<VideoLoungeLifecycleService> logger)
	{
		_context = context;
		_jobQueue = jobQueue;
		_loungeHub = loungeHub;
		_messengerHub = messengerHub;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task ScheduleIdleCheckAsync(int sessionId, CancellationToken cancellationToken = default)
	{
		var payload = JsonSerializer.Serialize(new { sessionId });
		return _jobQueue.ScheduleAsync(JobIdleCheck, payload, DateTime.UtcNow.AddHours(1), cancellationToken);
	}

	/// <inheritdoc />
	public async Task ProcessIdleCheckAsync(int sessionId, CancellationToken cancellationToken = default)
	{
		var session = await _context.FaceVideoLoungeSessions
			.Include(s => s.Participants)
			.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
		if (session == null || session.EndedAt != null)
			return;

		var active = session.Participants.Count(p => p.LeftAt == null);
		if (active > 0)
		{
			await ScheduleIdleCheckAsync(sessionId, cancellationToken);
			return;
		}

		if (DateTime.UtcNow - session.LastActivityAt < TimeSpan.FromHours(1))
		{
			await ScheduleIdleCheckAsync(sessionId, cancellationToken);
			return;
		}

		await EndSessionAsync(sessionId, "idle", cancellationToken);
	}

	/// <inheritdoc />
	public Task ScheduleStaleParticipantCheckAsync(int sessionId, int participantId, CancellationToken cancellationToken = default)
	{
		var payload = JsonSerializer.Serialize(new { sessionId, participantId });
		return _jobQueue.ScheduleAsync(JobStaleParticipant, payload, DateTime.UtcNow.AddSeconds(90), cancellationToken);
	}

	/// <inheritdoc />
	public async Task ProcessStaleParticipantCheckAsync(int sessionId, int participantId, CancellationToken cancellationToken = default)
	{
		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.Id == participantId && p.FaceVideoLoungeSessionId == sessionId, cancellationToken);
		if (row == null || row.LeftAt != null)
			return;

		if (DateTime.UtcNow - row.LastSeenAt < TimeSpan.FromSeconds(90))
			return;

		row.LeftAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		await _loungeHub.Clients.Group(VideoLoungeHub.SessionGroupName(sessionId))
			.SendAsync("LoungeParticipantLeft", sessionId, row.UserId, cancellationToken: cancellationToken);

		_logger.LogInformation("Stale participant removed session={SessionId} user={UserId}", sessionId, row.UserId);
	}

	/// <inheritdoc />
	public async Task NotifyMembersSessionStartedAsync(int loungeId, int sessionId, CancellationToken cancellationToken = default)
	{
		var lounge = await _context.FaceVideoLounges.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loungeId, cancellationToken);
		if (lounge == null)
			return;

		var memberIds = await _context.FaceVideoLoungeMembers.AsNoTracking()
			.Where(m => m.FaceVideoLoungeId == loungeId)
			.Select(m => m.UserId)
			.ToListAsync(cancellationToken);

		// Build all notifications first, single SaveChangesAsync (X10: one save vs one-per-member).
		var toNotify = memberIds
			.Where(id => id != lounge.CreatorUserId)
			.Select(id => new Notification
			{
				UserId = id,
				Title = "Video lounge is live",
				Message = $"\"{lounge.Title}\" started a live session.",
				Type = "video_lounge_live",
			})
			.ToList();

		foreach (var n in toNotify)
			_context.Notifications.Add(n);

		if (toNotify.Count > 0)
			await _context.SaveChangesAsync(cancellationToken);

		foreach (var n in toNotify)
			await _messengerHub.Clients.User(n.UserId).SendAsync(
				"ReceiveNotification",
				n.Id,
				n.Title,
				n.Message,
				n.Type,
				n.CreatedAt,
				cancellationToken);

		await _loungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(loungeId))
			.SendAsync("LoungePresenceUpdated", loungeId, sessionId, cancellationToken: cancellationToken);
	}

	/// <inheritdoc />
	public async Task EndSessionAsync(int sessionId, string reason, CancellationToken cancellationToken = default)
	{
		var session = await _context.FaceVideoLoungeSessions
			.Include(s => s.Participants)
			.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
		if (session == null || session.EndedAt != null)
			return;

		session.EndedAt = DateTime.UtcNow;
		foreach (var p in session.Participants.Where(x => x.LeftAt == null))
			p.LeftAt = DateTime.UtcNow;

		await _context.SaveChangesAsync(cancellationToken);

		await _loungeHub.Clients.Group(VideoLoungeHub.SessionGroupName(sessionId))
			.SendAsync("LoungeSessionEnded", sessionId, reason, cancellationToken: cancellationToken);

		await _loungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(session.FaceVideoLoungeId))
			.SendAsync("LoungePresenceUpdated", session.FaceVideoLoungeId, sessionId, cancellationToken: cancellationToken);

		_logger.LogInformation("Ended video lounge session {SessionId} reason={Reason}", sessionId, reason);
	}
}
