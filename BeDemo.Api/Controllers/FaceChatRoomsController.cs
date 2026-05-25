using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/faces/{faceId:int}/chat-rooms")]
[Authorize]
public class FaceChatRoomsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IChatRoomLifecycleService _lifecycle;
	private readonly IHubContext<MessengerHub> _messengerHub;
	private readonly IAccessEvaluator _access;
	private readonly ILogger<FaceChatRoomsController> _logger;

	public FaceChatRoomsController(
		ApplicationDbContext context,
		IChatRoomLifecycleService lifecycle,
		IHubContext<MessengerHub> messengerHub,
		IAccessEvaluator access,
		ILogger<FaceChatRoomsController> logger)
	{
		_context = context;
		_lifecycle = lifecycle;
		_messengerHub = messengerHub;
		_access = access;
		_logger = logger;
	}

	private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	private async Task<ApplicationUser?> GetUserTrackedAsync(CancellationToken ct) =>
		string.IsNullOrEmpty(UserId) ? null : await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId, ct);

	private static object RoomDto(
		FaceChatRoom r,
		bool isHostViewer,
		bool canParticipate,
		bool isMember,
		bool hasPendingRequest,
		int memberCount,
		int messageCount,
		int pendingJoinRequestCount) =>
		new
		{
			r.Id,
			r.FaceId,
			r.Title,
			r.Description,
			r.IsPublic,
			r.IsSystemManaged,
			r.CreatorUserId,
			r.CreatedAt,
			r.UpdatedAt,
			r.LastMessageAt,
			memberCount,
			messageCount,
			pendingJoinRequestCount,
			isHostViewer,
			canParticipate,
			isMember,
			hasPendingRequest,
		};

	/// <summary>List chat rooms for face (paginated envelope).</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		int faceId,
		[FromQuery] FaceChatRoomListQuery listQuery,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		IQueryable<FaceChatRoom> roomQuery = _context.FaceChatRooms.AsNoTracking().Where(r => r.FaceId == faceId);

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			roomQuery = roomQuery.Where(r =>
				EF.Functions.ILike(r.Title, pattern) ||
				(r.Description != null && EF.Functions.ILike(r.Description, pattern)));
		}

		if (listQuery.IsPublic.HasValue)
			roomQuery = roomQuery.Where(r => r.IsPublic == listQuery.IsPublic.Value);

		var totalCount = await roomQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var rooms = await ListSortApplicators
			.ApplyFaceChatRoomsSort(roomQuery, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var ids = rooms.Select(r => r.Id).ToList();
		var memberCounts = await _context.FaceChatRoomMembers
			.AsNoTracking()
			.Where(m => ids.Contains(m.FaceChatRoomId))
			.GroupBy(m => m.FaceChatRoomId)
			.Select(g => new { RoomId = g.Key, C = g.Count() })
			.ToDictionaryAsync(x => x.RoomId, x => x.C, cancellationToken);

		var myMemberships = (await _context.FaceChatRoomMembers
			.AsNoTracking()
			.Where(m => m.UserId == UserId && ids.Contains(m.FaceChatRoomId))
			.Select(m => m.FaceChatRoomId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var pending = (await _context.FaceChatRoomJoinRequests
			.AsNoTracking()
			.Where(j => j.UserId == UserId && j.Status == FaceChatRoomJoinRequestStatus.Pending && ids.Contains(j.FaceChatRoomId))
			.Select(j => j.FaceChatRoomId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var list = rooms.Select(r =>
		{
			var mc = memberCounts.GetValueOrDefault(r.Id);
			var member = myMemberships.Contains(r.Id);
			var canPart = !isHost;
			return RoomDto(r, isHost, canPart, member, pending.Contains(r.Id), mc, messageCount: 0, pendingJoinRequestCount: 0);
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(list, page, pageSize, totalCount, totalPages));
	}

	[HttpGet("{roomId:int}")]
	public async Task<IActionResult> Get(int faceId, int roomId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var room = await _context.FaceChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);
		var memberCount = await _context.FaceChatRoomMembers.CountAsync(m => m.FaceChatRoomId == roomId, cancellationToken);
		var isMember = await _context.FaceChatRoomMembers.AnyAsync(m => m.FaceChatRoomId == roomId && m.UserId == UserId, cancellationToken);
		var hasPending = await _context.FaceChatRoomJoinRequests.AnyAsync(
			j => j.FaceChatRoomId == roomId && j.UserId == UserId && j.Status == FaceChatRoomJoinRequestStatus.Pending,
			cancellationToken);

		var messageCount = await _context.FaceChatRoomMessages.CountAsync(m => m.FaceChatRoomId == roomId, cancellationToken);
		var pendingJoinRequestCount = await _context.FaceChatRoomJoinRequests.CountAsync(
			j => j.FaceChatRoomId == roomId && j.Status == FaceChatRoomJoinRequestStatus.Pending,
			cancellationToken);

		return Ok(RoomDto(room, isHost, canParticipate: !isHost, isMember, hasPending, memberCount, messageCount, pendingJoinRequestCount));
	}

	/// <summary>User-created room (requires face.ChatRoomsCreate, non-host).</summary>
	[HttpPost]
	public async Task<IActionResult> CreateUserRoom(int faceId, [FromBody] CreateFaceChatRoomDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		if (!face.ChatRoomsCreate)
			return Forbid();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var room = new FaceChatRoom
		{
			FaceId = faceId,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			IsPublic = dto.IsPublic,
			IsSystemManaged = false,
			CreatorUserId = UserId,
		};
		_context.FaceChatRooms.Add(room);
		await _context.SaveChangesAsync(cancellationToken);

		_context.FaceChatRoomMembers.Add(new FaceChatRoomMember
		{
			FaceChatRoomId = room.Id,
			UserId = UserId,
		});
		await _context.SaveChangesAsync(cancellationToken);

		await _lifecycle.ScheduleIdleCheckAsync(room.Id, cancellationToken);
		_logger.LogInformation("User {UserId} created chat room {RoomId} in face {FaceId}", UserId, room.Id, faceId);

		return CreatedAtAction(nameof(Get), new { faceId, roomId = room.Id }, new { room.Id });
	}

	/// <summary>System-managed room (global admin only). Always public.</summary>
	[HttpPost("system")]
	public async Task<IActionResult> CreateSystemRoom(int faceId, [FromBody] CreateSystemFaceChatRoomDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null || !await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
			return Forbid();

		var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var room = new FaceChatRoom
		{
			FaceId = faceId,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			IsPublic = true,
			IsSystemManaged = true,
			CreatorUserId = null,
		};
		_context.FaceChatRooms.Add(room);
		await _context.SaveChangesAsync(cancellationToken);

		await _lifecycle.ScheduleIdleCheckAsync(room.Id, cancellationToken);
		return CreatedAtAction(nameof(Get), new { faceId, roomId = room.Id }, new { room.Id });
	}

	[HttpPut("{roomId:int}")]
	public async Task<IActionResult> Update(int faceId, int roomId, [FromBody] UpdateFaceChatRoomDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null)
			return Unauthorized();

		var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();

		if (room.IsSystemManaged)
		{
			if (!await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
				return Forbid();
		}
		else
		{
			if (room.CreatorUserId != UserId)
				return Forbid();
			if (dto.IsPublic.HasValue)
				room.IsPublic = dto.IsPublic.Value;
		}

		if (!string.IsNullOrWhiteSpace(dto.Title))
			room.Title = dto.Title.Trim();
		if (dto.Description != null)
			room.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

		room.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { room.Id });
	}

	[HttpDelete("{roomId:int}")]
	public async Task<IActionResult> Delete(int faceId, int roomId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await GetUserTrackedAsync(cancellationToken);
		if (user == null)
			return Unauthorized();

		var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();

		if (room.IsSystemManaged)
		{
			if (!await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
				return Forbid();
		}
		else
		{
			if (room.CreatorUserId != UserId)
				return Forbid();
		}

		await _lifecycle.DeleteRoomCompletelyAsync(roomId, "deleted", notifyCreatorIdleExpiry: false, cancellationToken);
		return NoContent();
	}

	[HttpPost("{roomId:int}/join")]
	public async Task<IActionResult> JoinPublic(int faceId, int roomId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();
		if (!room.IsPublic)
			return BadRequest(new { error = "Room is private; use join request" });

		if (await _context.FaceChatRoomMembers.AnyAsync(m => m.FaceChatRoomId == roomId && m.UserId == UserId, cancellationToken))
			return Ok(new { alreadyMember = true });

		_context.FaceChatRoomMembers.Add(new FaceChatRoomMember { FaceChatRoomId = roomId, UserId = UserId });
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { joined = true });
	}

	[HttpPost("{roomId:int}/join-requests")]
	public async Task<IActionResult> RequestJoin(int faceId, int roomId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return Forbid();

		var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();
		if (room.IsPublic)
			return BadRequest(new { error = "Room is public; use join" });

		if (await _context.FaceChatRoomMembers.AnyAsync(m => m.FaceChatRoomId == roomId && m.UserId == UserId, cancellationToken))
			return BadRequest(new { error = "Already a member" });

		if (await _context.FaceChatRoomJoinRequests.AnyAsync(
				j => j.FaceChatRoomId == roomId && j.UserId == UserId && j.Status == FaceChatRoomJoinRequestStatus.Pending,
				cancellationToken))
			return Ok(new { pending = true });

		var req = new FaceChatRoomJoinRequest { FaceChatRoomId = roomId, UserId = UserId };
		_context.FaceChatRoomJoinRequests.Add(req);
		await _context.SaveChangesAsync(cancellationToken);

		if (!string.IsNullOrEmpty(room.CreatorUserId))
		{
			var requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
			var requesterName = requester != null ? $"{requester.FirstName} {requester.LastName}".Trim() : UserId;
			var notification = new Notification
			{
				UserId = room.CreatorUserId,
				Title = "Chat room join request",
				Message = $"{requesterName} wants to join \"{room.Title}\".",
				Type = "chat_room_join_request",
			};
			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync(cancellationToken);

			await _messengerHub.Clients.User(room.CreatorUserId).SendAsync(
				"ReceiveNotification",
				notification.Id,
				notification.Title,
				notification.Message,
				notification.Type,
				notification.CreatedAt,
				cancellationToken);
		}

		return Ok(new { requestId = req.Id });
	}

	[HttpPost("requests/{requestId:int}/approve")]
	public async Task<IActionResult> ApproveRequest(int faceId, int requestId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var req = await _context.FaceChatRoomJoinRequests
			.Include(r => r.Room)
			.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
		if (req == null || req.Room.FaceId != faceId)
			return NotFound();

		if (req.Room.CreatorUserId != UserId)
			return Forbid();

		if (req.Status != FaceChatRoomJoinRequestStatus.Pending)
			return BadRequest(new { error = "Request is not pending" });

		req.Status = FaceChatRoomJoinRequestStatus.Approved;
		req.ResolvedAt = DateTime.UtcNow;

		if (!await _context.FaceChatRoomMembers.AnyAsync(m => m.FaceChatRoomId == req.FaceChatRoomId && m.UserId == req.UserId, cancellationToken))
		{
			_context.FaceChatRoomMembers.Add(new FaceChatRoomMember
			{
				FaceChatRoomId = req.FaceChatRoomId,
				UserId = req.UserId,
			});
		}

		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { approved = true });
	}

	[HttpPost("requests/{requestId:int}/deny")]
	public async Task<IActionResult> DenyRequest(int faceId, int requestId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var req = await _context.FaceChatRoomJoinRequests
			.Include(r => r.Room)
			.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
		if (req == null || req.Room.FaceId != faceId)
			return NotFound();

		if (req.Room.CreatorUserId != UserId)
			return Forbid();

		if (req.Status != FaceChatRoomJoinRequestStatus.Pending)
			return BadRequest(new { error = "Request is not pending" });

		req.Status = FaceChatRoomJoinRequestStatus.Denied;
		req.ResolvedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { denied = true });
	}

	[HttpGet("{roomId:int}/messages")]
	public async Task<IActionResult> Messages(int faceId, int roomId, [FromQuery] ChatMessagesQuery messagesQuery, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var pageSize = messagesQuery.PageSize;
		var beforeId = messagesQuery.BeforeId;
		var room = await _context.FaceChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (room == null)
			return NotFound();

		var operatorInventory = CanManageAllFaces();
		if (!operatorInventory)
		{
			var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);
			var isMember = await _context.FaceChatRoomMembers.AnyAsync(
				m => m.FaceChatRoomId == roomId && m.UserId == UserId,
				cancellationToken);
			if (!isHost && !isMember)
				return Forbid();
		}

		// Operator admin tables: offset envelope (page >= 1, no beforeId).
		if (messagesQuery.Page >= 1 && !beforeId.HasValue)
		{
			if (!operatorInventory)
				return Forbid();

			var page = messagesQuery.Page;
			var messageQuery = _context.FaceChatRoomMessages.AsNoTracking()
				.Where(m => m.FaceChatRoomId == roomId);

			if (!string.IsNullOrWhiteSpace(messagesQuery.Search))
			{
				var term = messagesQuery.Search.Trim();
				messageQuery = messageQuery.Where(m =>
					m.Content.Contains(term)
					|| m.SenderUserId.Contains(term)
					|| (m.Sender.FirstName + " " + m.Sender.LastName).Contains(term)
					|| (m.Sender.Email != null && m.Sender.Email.Contains(term)));
			}

			var totalCount = await messageQuery.CountAsync(cancellationToken);
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var desc = SortRules.IsDescending(messagesQuery.SortDir);
			var ordered = (messagesQuery.SortBy?.ToLowerInvariant()) switch
			{
				"sentat" => desc
					? messageQuery.OrderByDescending(m => m.SentAt)
					: messageQuery.OrderBy(m => m.SentAt),
				"senderuserid" => desc
					? messageQuery.OrderByDescending(m => m.SenderUserId)
					: messageQuery.OrderBy(m => m.SenderUserId),
				_ => desc
					? messageQuery.OrderByDescending(m => m.Id)
					: messageQuery.OrderBy(m => m.Id),
			};

			var pageItems = await ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(m => new
				{
					m.Id,
					m.SenderUserId,
					SenderDisplayName = (m.Sender.FirstName + " " + m.Sender.LastName).Trim(),
					m.Content,
					m.SentAt,
				})
				.ToListAsync(cancellationToken);

			return Ok(ListPaginationHelper.BuildEnvelope(pageItems, page, pageSize, totalCount, totalPages));
		}

		// Portal / live chat: cursor batch (newest-first slice, reversed to chronological).
		var q = _context.FaceChatRoomMessages.AsNoTracking().Where(m => m.FaceChatRoomId == roomId);
		if (beforeId.HasValue)
			q = q.Where(m => m.Id < beforeId.Value);

		var items = await q
			.OrderByDescending(m => m.Id)
			.Take(pageSize)
			.Select(m => new
			{
				m.Id,
				m.SenderUserId,
				SenderDisplayName = (m.Sender.FirstName + " " + m.Sender.LastName).Trim(),
				m.Content,
				m.SentAt,
			})
			.ToListAsync(cancellationToken);

		items.Reverse();
		return Ok(items);
	}

	/// <summary>Operator inventory: paginated room members (joined users at query time).</summary>
	[HttpGet("{roomId:int}/members")]
	public async Task<IActionResult> ListMembers(
		int faceId,
		int roomId,
		[FromQuery] FaceChatRoomMembersListQuery listQuery,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (!CanManageAllFaces())
			return Forbid();

		var roomExists = await _context.FaceChatRooms.AsNoTracking()
			.AnyAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (!roomExists)
			return NotFound();

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var memberQuery = _context.FaceChatRoomMembers.AsNoTracking()
			.Where(m => m.FaceChatRoomId == roomId);

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var term = listQuery.Search.Trim();
			var matchingUserIds = await _context.Users.AsNoTracking()
				.Where(u =>
					u.Id.Contains(term)
					|| (u.FirstName + " " + u.LastName).Contains(term)
					|| (u.Email != null && u.Email.Contains(term)))
				.Select(u => u.Id)
				.ToListAsync(cancellationToken);
			memberQuery = memberQuery.Where(m =>
				m.UserId.Contains(term) || matchingUserIds.Contains(m.UserId));
		}

		var totalCount = await memberQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var desc = SortRules.IsDescending(listQuery.SortDir);
		var ordered = (listQuery.SortBy?.ToLowerInvariant()) switch
		{
			"userid" => desc
				? memberQuery.OrderByDescending(m => m.UserId)
				: memberQuery.OrderBy(m => m.UserId),
			_ => desc
				? memberQuery.OrderByDescending(m => m.JoinedAt)
				: memberQuery.OrderBy(m => m.JoinedAt),
		};

		var members = await ordered
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(m => new { m.UserId, m.JoinedAt })
			.ToListAsync(cancellationToken);

		var userIds = members.Select(m => m.UserId).ToList();
		var users = await _context.Users.AsNoTracking()
			.Where(u => userIds.Contains(u.Id))
			.Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
			.ToDictionaryAsync(u => u.Id, cancellationToken);

		var items = members.Select(m =>
		{
			users.TryGetValue(m.UserId, out var u);
			var displayName = u != null
				? $"{u.FirstName} {u.LastName}".Trim()
				: m.UserId;
			if (string.IsNullOrWhiteSpace(displayName))
				displayName = u?.Email ?? m.UserId;
			return new
			{
				userId = m.UserId,
				displayName,
				joinedAt = m.JoinedAt,
			};
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
	}

	/// <summary>Operator inventory: pending join requests for a private room.</summary>
	[HttpGet("{roomId:int}/join-requests")]
	public async Task<IActionResult> ListJoinRequests(
		int faceId,
		int roomId,
		[FromQuery] FaceChatRoomJoinRequestsListQuery listQuery,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (!CanManageAllFaces())
			return Forbid();

		var roomExists = await _context.FaceChatRooms.AsNoTracking()
			.AnyAsync(r => r.Id == roomId && r.FaceId == faceId, cancellationToken);
		if (!roomExists)
			return NotFound();

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var requestQuery = _context.FaceChatRoomJoinRequests.AsNoTracking()
			.Where(j => j.FaceChatRoomId == roomId && j.Status == FaceChatRoomJoinRequestStatus.Pending);

		var totalCount = await requestQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var requests = await requestQuery
			.OrderByDescending(j => j.CreatedAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(j => new { j.Id, j.UserId, j.CreatedAt, j.Status })
			.ToListAsync(cancellationToken);

		var userIds = requests.Select(r => r.UserId).ToList();
		var users = await _context.Users.AsNoTracking()
			.Where(u => userIds.Contains(u.Id))
			.Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
			.ToDictionaryAsync(u => u.Id, cancellationToken);

		var items = requests.Select(r =>
		{
			users.TryGetValue(r.UserId, out var u);
			var displayName = u != null
				? $"{u.FirstName} {u.LastName}".Trim()
				: r.UserId;
			if (string.IsNullOrWhiteSpace(displayName))
				displayName = u?.Email ?? r.UserId;
			return new
			{
				requestId = r.Id,
				userId = r.UserId,
				displayName,
				createdAt = r.CreatedAt,
				status = r.Status.ToString(),
			};
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
	}
}
