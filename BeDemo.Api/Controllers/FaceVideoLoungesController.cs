using System.Security.Claims;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Standalone VideoLounge REST API — not coupled to <see cref="FaceChatRoomsController"/>.
/// Members: CRUD, join, live session. Operators use <see cref="OperatorContentController"/> for stealth/kick.
/// </summary>
[ApiController]
[Route("api/faces/{faceId:int}/video-lounges")]
[Authorize]
public class FaceVideoLoungesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IVideoLoungeLifecycleService _lifecycle;
	private readonly IVideoLoungeTokenService _tokens;
	private readonly IHubContext<VideoLoungeHub> _loungeHub;
	private readonly IHubContext<MessengerHub> _messengerHub;
	private readonly IAccessEvaluator _access;
	private readonly ILogger<FaceVideoLoungesController> _logger;

	public FaceVideoLoungesController(
		ApplicationDbContext context,
		IVideoLoungeLifecycleService lifecycle,
		IVideoLoungeTokenService tokens,
		IHubContext<VideoLoungeHub> loungeHub,
		IHubContext<MessengerHub> messengerHub,
		IAccessEvaluator access,
		ILogger<FaceVideoLoungesController> logger)
	{
		_context = context;
		_lifecycle = lifecycle;
		_tokens = tokens;
		_loungeHub = loungeHub;
		_messengerHub = messengerHub;
		_access = access;
		_logger = logger;
	}

	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	private async Task<ApplicationUser?> GetUserTrackedAsync(CancellationToken ct) =>
		string.IsNullOrEmpty(UserId) ? null : await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId, ct);

	private async Task<FaceVideoLoungeSession?> GetActiveSessionAsync(int loungeId, CancellationToken ct) =>
		await _context.FaceVideoLoungeSessions
			.FirstOrDefaultAsync(s => s.FaceVideoLoungeId == loungeId && s.EndedAt == null, ct);

	private async Task<Dictionary<string, (string DisplayName, string? AvatarUrl)>> GetUserDisplayMapAsync(
		IEnumerable<string> userIds,
		CancellationToken ct)
	{
		var ids = userIds.Distinct().ToList();
		var users = await _context.Users.AsNoTracking()
			.Where(u => ids.Contains(u.Id))
			.Select(u => new { u.Id, u.FirstName, u.LastName })
			.ToListAsync(ct);

		return users.ToDictionary(
			u => u.Id,
			u => ($"{u.FirstName} {u.LastName}".Trim(), (string?)null));
	}

	private static object LoungeDto(
		FaceVideoLounge r,
		bool isHostViewer,
		bool canConnect,
		bool isMember,
		bool hasPendingRequest,
		int memberCount,
		bool hasLiveSession,
		int liveParticipantCount) =>
		new
		{
			r.Id,
			r.FaceId,
			r.Title,
			r.Description,
			r.IsPublic,
			r.IsSystemManaged,
			r.CreatorUserId,
			r.MaxParticipants,
			r.CreatedAt,
			r.UpdatedAt,
			memberCount,
			hasLiveSession,
			liveParticipantCount,
			isHostViewer,
			canConnect,
			isMember,
			hasPendingRequest,
		};

	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> List(
		int faceId,
		[FromQuery] FaceVideoLoungeListQuery listQuery,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		IQueryable<FaceVideoLounge> loungeQuery = _context.FaceVideoLounges.AsNoTracking().Where(r => r.FaceId == faceId);

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			loungeQuery = loungeQuery.Where(r =>
				EF.Functions.ILike(r.Title, pattern) ||
				(r.Description != null && EF.Functions.ILike(r.Description, pattern)));
		}

		if (listQuery.IsPublic.HasValue)
			loungeQuery = loungeQuery.Where(r => r.IsPublic == listQuery.IsPublic.Value);

		var totalCount = await loungeQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var lounges = await ListSortApplicators
			.ApplyFaceVideoLoungesSort(loungeQuery, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var ids = lounges.Select(r => r.Id).ToList();
		var memberCounts = await _context.FaceVideoLoungeMembers
			.AsNoTracking()
			.Where(m => ids.Contains(m.FaceVideoLoungeId))
			.GroupBy(m => m.FaceVideoLoungeId)
			.Select(g => new { LoungeId = g.Key, C = g.Count() })
			.ToDictionaryAsync(x => x.LoungeId, x => x.C, cancellationToken);

		var activeSessions = await _context.FaceVideoLoungeSessions.AsNoTracking()
			.Where(s => ids.Contains(s.FaceVideoLoungeId) && s.EndedAt == null)
			.Select(s => s.Id)
			.ToListAsync(cancellationToken);

		var sessionByLounge = await _context.FaceVideoLoungeSessions.AsNoTracking()
			.Where(s => ids.Contains(s.FaceVideoLoungeId) && s.EndedAt == null)
			.ToDictionaryAsync(s => s.FaceVideoLoungeId, s => s.Id, cancellationToken);

		// Batch all participant counts into one grouped query instead of one CountAsync per lounge (X10 N+1).
		Dictionary<int, int> liveCounts;
		if (sessionByLounge.Count == 0)
		{
			liveCounts = [];
		}
		else
		{
			var sessionIds = sessionByLounge.Values.ToList();
			var countsBySession = await _context.FaceVideoLoungeSessionParticipants.AsNoTracking()
				.Where(p => sessionIds.Contains(p.FaceVideoLoungeSessionId) && p.LeftAt == null && p.IsListedInPublicRoster)
				.GroupBy(p => p.FaceVideoLoungeSessionId)
				.Select(g => new { g.Key, C = g.Count() })
				.ToDictionaryAsync(x => x.Key, x => x.C, cancellationToken);
			liveCounts = sessionByLounge.ToDictionary(kv => kv.Key, kv => countsBySession.GetValueOrDefault(kv.Value));
		}

		var myMemberships = (await _context.FaceVideoLoungeMembers
			.AsNoTracking()
			.Where(m => m.UserId == UserId && ids.Contains(m.FaceVideoLoungeId))
			.Select(m => m.FaceVideoLoungeId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var pending = (await _context.FaceVideoLoungeJoinRequests
			.AsNoTracking()
			.Where(j => j.UserId == UserId && j.Status == FaceVideoLoungeJoinRequestStatus.Pending && ids.Contains(j.FaceVideoLoungeId))
			.Select(j => j.FaceVideoLoungeId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var list = lounges.Select(r =>
		{
			var mc = memberCounts.GetValueOrDefault(r.Id);
			var member = myMemberships.Contains(r.Id);
			var hasLive = sessionByLounge.ContainsKey(r.Id);
			var liveN = liveCounts.GetValueOrDefault(r.Id);
			return LoungeDto(r, isHost, canConnect: !isHost && member, member, pending.Contains(r.Id), mc, hasLive, liveN);
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(list, page, pageSize, totalCount, totalPages));
	}

	[HttpGet("{loungeId:int}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Get(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Id == loungeId && r.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);
		var memberCount = await _context.FaceVideoLoungeMembers.CountAsync(m => m.FaceVideoLoungeId == loungeId, cancellationToken);
		var isMember = await _context.FaceVideoLoungeMembers.AnyAsync(m => m.FaceVideoLoungeId == loungeId && m.UserId == UserId, cancellationToken);
		var hasPending = await _context.FaceVideoLoungeJoinRequests.AnyAsync(
			j => j.FaceVideoLoungeId == loungeId && j.UserId == UserId && j.Status == FaceVideoLoungeJoinRequestStatus.Pending,
			cancellationToken);

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		var liveCount = 0;
		if (session != null)
		{
			liveCount = await _context.FaceVideoLoungeSessionParticipants.CountAsync(
				p => p.FaceVideoLoungeSessionId == session.Id && p.LeftAt == null && p.IsListedInPublicRoster,
				cancellationToken);
		}

		return Ok(LoungeDto(lounge, isHost, canConnect: !isHost && isMember, isMember, hasPending, memberCount, session != null, liveCount));
	}

	[HttpPost]
	[ProducesResponseType(typeof(CreatedEntityDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateUserLounge(int faceId, [FromBody] CreateFaceVideoLoungeDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		if (!face.VideoLoungesCreate)
			return Forbid();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var lounge = new FaceVideoLounge
		{
			FaceId = faceId,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			IsPublic = dto.IsPublic,
			IsSystemManaged = false,
			CreatorUserId = UserId,
			MaxParticipants = Math.Clamp(dto.MaxParticipants, 2, 50),
		};
		_context.FaceVideoLounges.Add(lounge);
		await _context.SaveChangesAsync(cancellationToken);

		_context.FaceVideoLoungeMembers.Add(new FaceVideoLoungeMember
		{
			FaceVideoLoungeId = lounge.Id,
			UserId = UserId,
		});
		await _context.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(Get), new { faceId, loungeId = lounge.Id }, new CreatedEntityDto { Id = lounge.Id });
	}

	[HttpPost("system")]
	[ProducesResponseType(typeof(CreatedEntityDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateSystemLounge(int faceId, [FromBody] CreateSystemFaceVideoLoungeDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null || !await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
			return Forbid();

		var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var lounge = new FaceVideoLounge
		{
			FaceId = faceId,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			IsPublic = true,
			IsSystemManaged = true,
			CreatorUserId = null,
			MaxParticipants = Math.Clamp(dto.MaxParticipants, 2, 50),
		};
		_context.FaceVideoLounges.Add(lounge);
		await _context.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(Get), new { faceId, loungeId = lounge.Id }, new CreatedEntityDto { Id = lounge.Id });
	}

	[HttpPut("{loungeId:int}")]
	[ProducesResponseType(typeof(CreatedEntityDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(int faceId, int loungeId, [FromBody] UpdateFaceVideoLoungeDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null)
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges.FirstOrDefaultAsync(r => r.Id == loungeId && r.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		if (lounge.IsSystemManaged)
		{
			if (!await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
				return Forbid();
		}
		else if (lounge.CreatorUserId != UserId)
		{
			return Forbid();
		}

		if (!string.IsNullOrWhiteSpace(dto.Title))
			lounge.Title = dto.Title.Trim();
		if (dto.Description != null)
			lounge.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		if (dto.IsPublic.HasValue)
			lounge.IsPublic = dto.IsPublic.Value;
		if (dto.MaxParticipants.HasValue)
			lounge.MaxParticipants = Math.Clamp(dto.MaxParticipants.Value, 2, 50);

		lounge.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new CreatedEntityDto { Id = lounge.Id });
	}

	[HttpDelete("{loungeId:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Delete(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null)
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges
			.Include(l => l.Sessions)
			.FirstOrDefaultAsync(r => r.Id == loungeId && r.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		if (lounge.IsSystemManaged)
		{
			if (!await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
				return Forbid();
		}
		else if (lounge.CreatorUserId != UserId)
		{
			return Forbid();
		}

		foreach (var session in lounge.Sessions.Where(s => s.EndedAt == null))
			await _lifecycle.EndSessionAsync(session.Id, "lounge_deleted", cancellationToken);

		// ExecuteDeleteAsync issues DELETE WHERE directly — no round-trip to materialise rows first.
		await _context.FaceVideoLoungeJoinRequests.Where(x => x.FaceVideoLoungeId == loungeId).ExecuteDeleteAsync(cancellationToken);
		await _context.FaceVideoLoungeMembers.Where(x => x.FaceVideoLoungeId == loungeId).ExecuteDeleteAsync(cancellationToken);
		_context.FaceVideoLounges.Remove(lounge);
		await _context.SaveChangesAsync(cancellationToken);

		return NoContent();
	}

	[HttpPost("{loungeId:int}/join")]
	[ProducesResponseType(typeof(JoinResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> JoinPublic(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var lounge = await _context.FaceVideoLounges.FirstOrDefaultAsync(r => r.Id == loungeId && r.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();
		if (!lounge.IsPublic)
			return BadRequest(new ErrorResponseDto { Error = "Lounge is private; use join request" });

		if (await _context.FaceVideoLoungeMembers.AnyAsync(m => m.FaceVideoLoungeId == loungeId && m.UserId == UserId, cancellationToken))
			return Ok(new JoinResultDto { AlreadyMember = true });

		_context.FaceVideoLoungeMembers.Add(new FaceVideoLoungeMember { FaceVideoLoungeId = loungeId, UserId = UserId });
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new JoinResultDto { Joined = true });
	}

	[HttpPost("{loungeId:int}/join-requests")]
	[ProducesResponseType(typeof(JoinRequestResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> RequestJoin(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var lounge = await _context.FaceVideoLounges.FirstOrDefaultAsync(r => r.Id == loungeId && r.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();
		if (lounge.IsPublic)
			return BadRequest(new ErrorResponseDto { Error = "Lounge is public; use join" });

		if (await _context.FaceVideoLoungeMembers.AnyAsync(m => m.FaceVideoLoungeId == loungeId && m.UserId == UserId, cancellationToken))
			return BadRequest(new ErrorResponseDto { Error = "Already a member" });

		if (await _context.FaceVideoLoungeJoinRequests.AnyAsync(
				j => j.FaceVideoLoungeId == loungeId && j.UserId == UserId && j.Status == FaceVideoLoungeJoinRequestStatus.Pending,
				cancellationToken))
			return Ok(new JoinRequestResultDto { Pending = true });

		var req = new FaceVideoLoungeJoinRequest { FaceVideoLoungeId = loungeId, UserId = UserId };
		_context.FaceVideoLoungeJoinRequests.Add(req);
		await _context.SaveChangesAsync(cancellationToken);

		if (!string.IsNullOrEmpty(lounge.CreatorUserId))
		{
			var requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
			var requesterName = requester != null ? $"{requester.FirstName} {requester.LastName}".Trim() : UserId;
			var notification = new Notification
			{
				UserId = lounge.CreatorUserId,
				Title = "Video lounge join request",
				Message = $"{requesterName} wants to join \"{lounge.Title}\".",
				Type = "video_lounge_join_request",
			};
			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync(cancellationToken);

			await _messengerHub.Clients.User(lounge.CreatorUserId).SendAsync(
				"ReceiveNotification",
				notification.Id,
				notification.Title,
				notification.Message,
				notification.Type,
				notification.CreatedAt,
				cancellationToken);
		}

		return Ok(new JoinRequestResultDto { RequestId = req.Id });
	}

	/// <summary>Live roster and counts; stealth omitted for members, included for operators.</summary>
	[HttpGet("{loungeId:int}/live")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLive(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == loungeId && l.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		if (session == null)
			return Ok(new VideoLoungeNoSessionDto());

		var active = await _context.FaceVideoLoungeSessionParticipants
			.AsNoTracking()
			.Where(p => p.FaceVideoLoungeSessionId == session.Id && p.LeftAt == null)
			.ToListAsync(cancellationToken);

		var userMap = await GetUserDisplayMapAsync(active.Select(p => p.UserId), cancellationToken);

		if (CanManageAllFaces())
		{
			var op = VideoLoungeLiveSnapshot.BuildOperator(active, userMap);
			return Ok(op);
		}

		return Ok(VideoLoungeLiveSnapshot.BuildPublic(active, userMap));
	}

	/// <summary>Member starts a live session (non-host, must be lounge member).</summary>
	[HttpPost("{loungeId:int}/live/start")]
	[ProducesResponseType(typeof(SessionIdResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> StartLive(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var lounge = await _context.FaceVideoLounges.FirstOrDefaultAsync(l => l.Id == loungeId && l.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		if (!await _context.FaceVideoLoungeMembers.AnyAsync(m => m.FaceVideoLoungeId == loungeId && m.UserId == UserId, cancellationToken))
			return Forbid();

		if (await GetActiveSessionAsync(loungeId, cancellationToken) != null)
			return Conflict(new ErrorResponseDto { Error = "Live session already active" });

		var session = new FaceVideoLoungeSession
		{
			FaceVideoLoungeId = loungeId,
			StartedByUserId = UserId,
			StartedAt = DateTime.UtcNow,
			LastActivityAt = DateTime.UtcNow,
		};
		_context.FaceVideoLoungeSessions.Add(session);
		await _context.SaveChangesAsync(cancellationToken);

		await _lifecycle.ScheduleIdleCheckAsync(session.Id, cancellationToken);
		await _lifecycle.NotifyMembersSessionStartedAsync(loungeId, session.Id, cancellationToken);

		return Ok(new SessionIdResultDto { SessionId = session.Id });
	}

	/// <summary>Connect to live SFU with Viewer, Listener, or Full join mode.</summary>
	[HttpPost("{loungeId:int}/live/join")]
	[ProducesResponseType(typeof(VideoLoungeJoinResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> JoinLive(
		int faceId,
		int loungeId,
		[FromBody] VideoLoungeLiveJoinDto dto,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (!VideoLoungeJoinModeParser.TryParseMemberMode(dto.JoinMode, out var mode))
			return BadRequest(new ErrorResponseDto { Error = "Invalid joinMode" });

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == loungeId && l.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		if (!await _context.FaceVideoLoungeMembers.AnyAsync(m => m.FaceVideoLoungeId == loungeId && m.UserId == UserId, cancellationToken))
			return Forbid();

		var session = await _context.FaceVideoLoungeSessions
			.FirstOrDefaultAsync(s => s.FaceVideoLoungeId == loungeId && s.EndedAt == null, cancellationToken);
		if (session == null)
			return Conflict(new ErrorResponseDto { Error = "No active live session" });

		var listedCount = await _context.FaceVideoLoungeSessionParticipants.CountAsync(
			p => p.FaceVideoLoungeSessionId == session.Id && p.LeftAt == null && p.IsListedInPublicRoster,
			cancellationToken);
		if (listedCount >= lounge.MaxParticipants)
			return Conflict(new ErrorResponseDto { Error = "Room is full" });

		var existing = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == UserId && p.LeftAt == null, cancellationToken);
		if (existing != null)
		{
			existing.JoinMode = mode;
			existing.AudioEnabled = mode != VideoLoungeJoinMode.Viewer;
			existing.VideoEnabled = mode == VideoLoungeJoinMode.Full;
			existing.LastSeenAt = DateTime.UtcNow;
		}
		else
		{
			existing = new FaceVideoLoungeSessionParticipant
			{
				FaceVideoLoungeSessionId = session.Id,
				UserId = UserId,
				JoinMode = mode,
				AudioEnabled = mode != VideoLoungeJoinMode.Viewer,
				VideoEnabled = mode == VideoLoungeJoinMode.Full,
				IsListedInPublicRoster = true,
			};
			_context.FaceVideoLoungeSessionParticipants.Add(existing);
		}

		session.LastActivityAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		await _lifecycle.ScheduleStaleParticipantCheckAsync(session.Id, existing.Id, cancellationToken);

		var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
		var displayName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : UserId;
		var tokenResult = _tokens.CreateToken(session.Id, UserId, displayName, mode);

		await _loungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(loungeId))
			.SendAsync("LoungePresenceUpdated", loungeId, session.Id, cancellationToken: cancellationToken);

		return Ok(new VideoLoungeJoinResultDto
		{
			SessionId = session.Id,
			JoinMode = mode.ToString(),
			Token = tokenResult.Token,
			ServerUrl = tokenResult.ServerUrl,
			RoomName = tokenResult.RoomName,
			IsStub = tokenResult.IsStub,
			ExpiresAtUtc = tokenResult.ExpiresAtUtc,
		});
	}

	[HttpPost("{loungeId:int}/live/leave")]
	[ProducesResponseType(typeof(LeftResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> LeaveLive(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		if (session == null)
			return Ok(new LeftResultDto { Left = false });

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == UserId && p.LeftAt == null, cancellationToken);
		if (row == null)
			return Ok(new LeftResultDto { Left = false });

		row.LeftAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		await _loungeHub.Clients.Group(VideoLoungeHub.SessionGroupName(session.Id))
			.SendAsync("LoungeParticipantLeft", session.Id, UserId, cancellationToken: cancellationToken);
		await _loungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(loungeId))
			.SendAsync("LoungePresenceUpdated", loungeId, session.Id, cancellationToken: cancellationToken);

		return Ok(new LeftResultDto { Left = true });
	}

	[HttpPost("{loungeId:int}/live/end")]
	[ProducesResponseType(typeof(EndedResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> EndLive(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == loungeId && l.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		if (session == null)
			return NotFound();

		if (session.StartedByUserId != UserId && lounge.CreatorUserId != UserId && !CanManageAllFaces())
			return Forbid();

		await _lifecycle.EndSessionAsync(session.Id, "ended", cancellationToken);
		return Ok(new EndedResultDto());
	}

	[HttpPost("{loungeId:int}/live/refresh-token")]
	[ProducesResponseType(typeof(VideoLoungeRefreshTokenResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> RefreshToken(
		int faceId,
		int loungeId,
		[FromBody] VideoLoungeLiveJoinDto dto,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (!VideoLoungeJoinModeParser.TryParseMemberMode(dto.JoinMode, out var mode))
			return BadRequest(new ErrorResponseDto { Error = "Invalid joinMode" });

		var lounge = await _context.FaceVideoLounges.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == loungeId && l.FaceId == faceId, cancellationToken);
		if (lounge == null)
			return NotFound();

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		if (session == null)
			return Conflict(new ErrorResponseDto { Error = "No active live session" });

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == UserId && p.LeftAt == null, cancellationToken);
		if (row == null)
			return Forbid();

		row.JoinMode = mode;
		row.LastSeenAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
		var displayName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : UserId;
		var tokenResult = _tokens.CreateToken(session.Id, UserId, displayName, row.JoinMode);

		return Ok(new VideoLoungeRefreshTokenResultDto
		{
			Token = tokenResult.Token,
			ServerUrl = tokenResult.ServerUrl,
			RoomName = tokenResult.RoomName,
			IsStub = tokenResult.IsStub,
			ExpiresAtUtc = tokenResult.ExpiresAtUtc,
		});
	}

	[HttpPost("{loungeId:int}/live/heartbeat")]
	[ProducesResponseType(typeof(OkResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Heartbeat(int faceId, int loungeId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var session = await GetActiveSessionAsync(loungeId, cancellationToken);
		if (session == null)
			return NotFound();

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == UserId && p.LeftAt == null, cancellationToken);
		if (row == null)
			return NotFound();

		row.LastSeenAt = DateTime.UtcNow;
		session.LastActivityAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		await _lifecycle.ScheduleStaleParticipantCheckAsync(session.Id, row.Id, cancellationToken);

		return Ok(new OkResultDto());
	}
}
