using System.Security.Claims;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.OperatorContent;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin operator content actions (album/reel/blog hard-delete shared by Remove + detail delete UI).</summary>
[ApiController]
[Route("api/operator-content")]
[Authorize]
public sealed class OperatorContentController : ControllerBase
{
	private readonly IAccessEvaluator _access;
	private readonly IOperatorAlbumManagementService _albums;
	private readonly IOperatorReelManagementService _reels;
	private readonly IOperatorBlogManagementService _blogs;
	private readonly IOperatorChatRoomManagementService _chatRooms;
	private readonly IOperatorProfileSocialManagementService _profiles;
	private readonly IOperatorStoryManagementService _stories;
	private readonly ApplicationDbContext _context;
	private readonly IVideoLoungeTokenService _videoLoungeTokens;
	private readonly IVideoLoungeLifecycleService _videoLoungeLifecycle;
	private readonly IHubContext<VideoLoungeHub> _videoLoungeHub;

	public OperatorContentController(
		IAccessEvaluator access,
		IOperatorAlbumManagementService albums,
		IOperatorReelManagementService reels,
		IOperatorBlogManagementService blogs,
		IOperatorChatRoomManagementService chatRooms,
		IOperatorProfileSocialManagementService profiles,
		IOperatorStoryManagementService stories,
		ApplicationDbContext context,
		IVideoLoungeTokenService videoLoungeTokens,
		IVideoLoungeLifecycleService videoLoungeLifecycle,
		IHubContext<VideoLoungeHub> videoLoungeHub)
	{
		_access = access;
		_albums = albums;
		_reels = reels;
		_blogs = blogs;
		_chatRooms = chatRooms;
		_profiles = profiles;
		_stories = stories;
		_context = context;
		_videoLoungeTokens = videoLoungeTokens;
		_videoLoungeLifecycle = videoLoungeLifecycle;
		_videoLoungeHub = videoLoungeHub;
	}

	private string? OperatorUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);

	private bool RequireManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>Hard-delete album (toolbar Remove and Delete album both use this).</summary>
	[HttpPost("albums/{id:int}/delete")]
	public async Task<IActionResult> HardDeleteAlbum(
		int id,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		await _albums.HardDeleteAlbumAsync(
			OperatorUserId,
			id,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return NoContent();
	}

	/// <summary>Delete one album media item; album row remains.</summary>
	[HttpPost("albums/{albumId:int}/media/{mediaId:int}/delete")]
	public async Task<IActionResult> DeleteAlbumMedia(
		int albumId,
		int mediaId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var ok = await _albums.DeleteAlbumMediaAsync(
			OperatorUserId,
			albumId,
			mediaId,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return ok ? NoContent() : NotFound(new { error = "Album or media not found" });
	}

	/// <summary>Hard-delete reel (toolbar Remove and Delete reel both use this).</summary>
	[HttpPost("reels/{id:int}/delete")]
	public async Task<IActionResult> HardDeleteReel(
		int id,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		await _reels.HardDeleteReelAsync(
			OperatorUserId,
			id,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return NoContent();
	}

	/// <summary>Hard-delete blog (toolbar Remove and Delete blog both use this).</summary>
	[HttpPost("blogs/{id:int}/delete")]
	public async Task<IActionResult> HardDeleteBlog(
		int id,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		await _blogs.HardDeleteBlogAsync(
			OperatorUserId,
			id,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return NoContent();
	}

	/// <summary>Delete one blog image; blog row remains.</summary>
	[HttpPost("blogs/{blogId:int}/images/{imageId:int}/delete")]
	public async Task<IActionResult> DeleteBlogImage(
		int blogId,
		int imageId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var ok = await _blogs.DeleteBlogImageAsync(
			OperatorUserId,
			blogId,
			imageId,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return ok ? NoContent() : NotFound(new { error = "Blog or image not found" });
	}

	/// <summary>Hard-delete face chat room (operator detail Delete room).</summary>
	[HttpPost("chat-rooms/{roomId:int}/delete")]
	public async Task<IActionResult> HardDeleteChatRoom(
		int roomId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		await _chatRooms.HardDeleteRoomAsync(
			OperatorUserId,
			roomId,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return NoContent();
	}

	/// <summary>
	/// Operator stealth-join: AdminStealth, subscribe-only token, hidden from portal roster.
	/// Requires <see cref="IAccessEvaluator.CanManageAllFaces"/> — not super-admin-only.
	/// </summary>
	[HttpPost("video-lounges/{loungeId:int}/live/stealth-join")]
	public async Task<IActionResult> StealthJoinVideoLounge(int loungeId, CancellationToken cancellationToken)
	{
		if (!RequireManageAllFaces())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var lounge = await _context.FaceVideoLounges.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loungeId, cancellationToken);
		if (lounge == null)
			return NotFound();

		var session = await _context.FaceVideoLoungeSessions
			.FirstOrDefaultAsync(s => s.FaceVideoLoungeId == loungeId && s.EndedAt == null, cancellationToken);
		if (session == null)
			return Conflict(new { error = "No active live session" });

		var existing = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == OperatorUserId && p.LeftAt == null, cancellationToken);
		if (existing == null)
		{
			existing = new FaceVideoLoungeSessionParticipant
			{
				FaceVideoLoungeSessionId = session.Id,
				UserId = OperatorUserId,
				JoinMode = VideoLoungeJoinMode.AdminStealth,
				AudioEnabled = false,
				VideoEnabled = false,
				IsListedInPublicRoster = false,
			};
			_context.FaceVideoLoungeSessionParticipants.Add(existing);
		}
		else
		{
			existing.JoinMode = VideoLoungeJoinMode.AdminStealth;
			existing.IsListedInPublicRoster = false;
			existing.AudioEnabled = false;
			existing.VideoEnabled = false;
		}

		existing.LastSeenAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == OperatorUserId, cancellationToken);
		var displayName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : OperatorUserId;
		var tokenResult = _videoLoungeTokens.CreateToken(session.Id, OperatorUserId, displayName, VideoLoungeJoinMode.AdminStealth);

		return Ok(new
		{
			sessionId = session.Id,
			joinMode = VideoLoungeJoinMode.AdminStealth.ToString(),
			token = tokenResult.Token,
			serverUrl = tokenResult.ServerUrl,
			roomName = tokenResult.RoomName,
			isStub = tokenResult.IsStub,
			expiresAtUtc = tokenResult.ExpiresAtUtc,
		});
	}

	/// <summary>Force one participant to leave live session and notify session group.</summary>
	[HttpPost("video-lounges/{loungeId:int}/live/kick/{userId}")]
	public async Task<IActionResult> KickVideoLoungeParticipant(int loungeId, string userId, CancellationToken cancellationToken)
	{
		if (!RequireManageAllFaces())
			return Forbid();

		var session = await _context.FaceVideoLoungeSessions
			.FirstOrDefaultAsync(s => s.FaceVideoLoungeId == loungeId && s.EndedAt == null, cancellationToken);
		if (session == null)
			return NotFound();

		var row = await _context.FaceVideoLoungeSessionParticipants
			.FirstOrDefaultAsync(p => p.FaceVideoLoungeSessionId == session.Id && p.UserId == userId && p.LeftAt == null, cancellationToken);
		if (row == null)
			return NotFound();

		row.LeftAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		await _videoLoungeHub.Clients.Group(VideoLoungeHub.SessionGroupName(session.Id))
			.SendAsync("LoungeParticipantKicked", session.Id, userId, cancellationToken: cancellationToken);
		await _videoLoungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(loungeId))
			.SendAsync("LoungePresenceUpdated", loungeId, session.Id, cancellationToken: cancellationToken);

		return Ok(new { kicked = true });
	}

	/// <summary>Kick all non-stealth participants; optional endSession query ends the live session.</summary>
	[HttpPost("video-lounges/{loungeId:int}/live/kick-all")]
	public async Task<IActionResult> KickAllVideoLoungeParticipants(
		int loungeId,
		[FromQuery] bool endSession = false,
		CancellationToken cancellationToken = default)
	{
		if (!RequireManageAllFaces())
			return Forbid();

		var session = await _context.FaceVideoLoungeSessions
			.Include(s => s.Participants)
			.FirstOrDefaultAsync(s => s.FaceVideoLoungeId == loungeId && s.EndedAt == null, cancellationToken);
		if (session == null)
			return NotFound();

		foreach (var p in session.Participants.Where(x => x.LeftAt == null && x.JoinMode != VideoLoungeJoinMode.AdminStealth))
		{
			p.LeftAt = DateTime.UtcNow;
			await _videoLoungeHub.Clients.Group(VideoLoungeHub.SessionGroupName(session.Id))
				.SendAsync("LoungeParticipantKicked", session.Id, p.UserId, cancellationToken: cancellationToken);
		}

		await _context.SaveChangesAsync(cancellationToken);

		if (endSession)
			await _videoLoungeLifecycle.EndSessionAsync(session.Id, "operator_kick_all", cancellationToken);
		else
		{
			await _videoLoungeHub.Clients.Group(VideoLoungeHub.LoungeGroupName(loungeId))
				.SendAsync("LoungePresenceUpdated", loungeId, session.Id, cancellationToken: cancellationToken);
		}

		return Ok(new { kickedAll = true, endSession });
	}

	/// <summary>Remove one profile comment (operator profile detail row delete).</summary>
	[HttpPost("profile-comments/{commentId:int}/delete")]
	public async Task<IActionResult> DeleteProfileComment(
		int commentId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var ok = await _profiles.DeleteCommentAsync(
			OperatorUserId,
			commentId,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);
		return ok ? NoContent() : NotFound(new { error = "Comment not found" });
	}

	/// <summary>Remove one profile review (operator profile detail row delete).</summary>
	[HttpPost("profile-reviews/{reviewId:int}/delete")]
	public async Task<IActionResult> DeleteProfileReview(
		int reviewId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var ok = await _profiles.DeleteReviewAsync(
			OperatorUserId,
			reviewId,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);
		return ok ? NoContent() : NotFound(new { error = "Review not found" });
	}

	/// <summary>Hard-delete story (operator detail Delete story).</summary>
	[HttpPost("stories/{id:int}/delete")]
	public async Task<IActionResult> HardDeleteStory(
		int id,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		await _stories.HardDeleteStoryAsync(
			OperatorUserId,
			id,
			request.FaceId,
			request.Reason,
			request.UserMessage,
			cancellationToken);

		return NoContent();
	}

	/// <summary>Delete one story image; story row remains (no platform DM).</summary>
	[HttpPost("stories/{storyId:int}/images/{imageId:int}/delete")]
	public async Task<IActionResult> DeleteStoryImage(
		int storyId,
		int imageId,
		[FromBody] OperatorAlbumDeleteRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		try
		{
			var ok = await _stories.DeleteStoryImageAsync(
				OperatorUserId,
				storyId,
				imageId,
				request.FaceId,
				request.Reason,
				request.UserMessage,
				cancellationToken);

			return ok ? NoContent() : NotFound(new { error = "Story or image not found" });
		}
		catch (InvalidOperationException ex) when (ex.Message == "image_delete_blocked_live")
		{
			return BadRequest(new { error = "image_delete_blocked_live" });
		}
	}
}
