using System.Security.Claims;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Hubs;

/// <summary>
/// SignalR hub for VideoLounge presence and kicks — separate from <see cref="ChatRoomHub"/>.
/// Groups: <c>video-lounge_{loungeId}</c> (lobby updates), <c>video-lounge-session_{sessionId}</c> (live).
/// </summary>
[Authorize]
public class VideoLoungeHub : Hub
{
	private readonly ApplicationDbContext _context;
	private readonly IVideoLoungeLifecycleService _lifecycle;
	private readonly IFaceScopeContext _faceScope;
	private readonly ILogger<VideoLoungeHub> _logger;

	public VideoLoungeHub(
		ApplicationDbContext context,
		IVideoLoungeLifecycleService lifecycle,
		IFaceScopeContext faceScope,
		ILogger<VideoLoungeHub> logger)
	{
		_context = context;
		_lifecycle = lifecycle;
		_faceScope = faceScope;
		_logger = logger;
	}

	private string? UserId =>
		Context.UserIdentifier ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

	public static string LoungeGroupName(int loungeId) => $"video-lounge_{loungeId}";

	public static string SessionGroupName(int sessionId) => $"video-lounge-session_{sessionId}";

	/// <summary>Subscribe to lounge lobby presence updates (member or operator).</summary>
	public async Task JoinLounge(int faceVideoLoungeId)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == faceVideoLoungeId);
		if (lounge == null)
			return;

		if (_faceScope.IsAvailable && !_faceScope.IsAdminFaceScope && lounge.FaceId != _faceScope.FaceId)
			return;

		await Groups.AddToGroupAsync(Context.ConnectionId, LoungeGroupName(faceVideoLoungeId));
	}

	public async Task LeaveLounge(int faceVideoLoungeId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, LoungeGroupName(faceVideoLoungeId));
	}

	/// <summary>Join live session group after REST live/join succeeded.</summary>
	public async Task JoinSession(int sessionId, string joinMode)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		if (!VideoLoungeJoinModeParser.TryParseMemberMode(joinMode, out var mode)
			&& !Enum.TryParse(joinMode, true, out mode))
			return;

		var session = await _context.FaceVideoLoungeSessions
			.Include(s => s.Lounge)
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == sessionId && s.EndedAt == null);
		if (session == null)
			return;

		if (_faceScope.IsAvailable && !_faceScope.IsAdminFaceScope && session.Lounge.FaceId != _faceScope.FaceId)
			return;

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, session.Lounge.FaceId))
			return;

		var isMember = await _context.FaceVideoLoungeMembers
			.AnyAsync(m => m.FaceVideoLoungeId == session.FaceVideoLoungeId && m.UserId == UserId);
		var isActiveParticipant = await _context.FaceVideoLoungeSessionParticipants
			.AnyAsync(p => p.FaceVideoLoungeSessionId == sessionId && p.UserId == UserId && p.LeftAt == null);

		if (!isMember && !isActiveParticipant && mode != VideoLoungeJoinMode.AdminStealth)
			return;

		await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroupName(sessionId));
		await Clients.Group(SessionGroupName(sessionId)).SendAsync(
			"LoungeParticipantJoined",
			sessionId,
			UserId,
			mode.ToString());
	}

	/// <summary>Heartbeat while connected — updates LastSeenAt and reschedules stale job.</summary>
	public async Task Heartbeat(int sessionId)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p =>
				p.FaceVideoLoungeSessionId == sessionId && p.UserId == UserId && p.LeftAt == null);
		if (row == null)
			return;

		row.LastSeenAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		var session = await _context.FaceVideoLoungeSessions.FindAsync(sessionId);
		if (session != null)
		{
			session.LastActivityAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();
		}

		await _lifecycle.ScheduleStaleParticipantCheckAsync(sessionId, row.Id);
	}

	/// <summary>Viewer cannot enable publish — rejects mic/cam toggles for non-speaker modes.</summary>
	public async Task UpdatePresence(int sessionId, bool? audioEnabled, bool? videoEnabled)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p =>
				p.FaceVideoLoungeSessionId == sessionId && p.UserId == UserId && p.LeftAt == null);
		if (row == null)
			return;

		if (row.JoinMode == VideoLoungeJoinMode.Viewer || row.JoinMode == VideoLoungeJoinMode.AdminStealth)
		{
			if (audioEnabled == true || videoEnabled == true)
				return;
		}

		if (row.JoinMode == VideoLoungeJoinMode.Listener && videoEnabled == true)
			return;

		if (audioEnabled.HasValue)
			row.AudioEnabled = audioEnabled.Value;
		if (videoEnabled.HasValue)
			row.VideoEnabled = videoEnabled.Value;

		row.LastSeenAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		var loungeId = await _context.FaceVideoLoungeSessions.AsNoTracking()
			.Where(s => s.Id == sessionId)
			.Select(s => s.FaceVideoLoungeId)
			.FirstOrDefaultAsync();

		await Clients.Group(SessionGroupName(sessionId))
			.SendAsync("LoungePresenceUpdated", loungeId, sessionId);
	}

	public async Task LeaveSession(int sessionId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroupName(sessionId));
	}
}
