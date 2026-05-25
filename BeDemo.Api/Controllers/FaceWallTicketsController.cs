using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/faces/{faceId:int}/wall-tickets")]
[Authorize]
public class FaceWallTicketsController : ControllerBase
{
	public const int MaxTicketsPerUserPerFace = 20;
	public const int MaxDescriptionLength = 8000;
	public const int MaxCommentLength = 255;

	private readonly ApplicationDbContext _context;
	private readonly ILogger<FaceWallTicketsController> _logger;

	public FaceWallTicketsController(ApplicationDbContext context, ILogger<FaceWallTicketsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	private static string StatusString(FaceWallTicketStatus s) =>
		s switch
		{
			FaceWallTicketStatus.Active => "active",
			FaceWallTicketStatus.Approved => "approved",
			FaceWallTicketStatus.Denied => "denied",
			_ => "active",
		};

	private static bool IsFrozen(FaceWallTicketStatus status) =>
		status is FaceWallTicketStatus.Approved or FaceWallTicketStatus.Denied;

	[HttpGet]
	public async Task<IActionResult> List(
		int faceId,
		[FromQuery] WallTicketListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new { error = "Face not found" });

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		var query = _context.FaceWallTickets
			.AsNoTracking()
			.Include(t => t.Creator)
			.Where(t => t.FaceId == faceId);

		var total = await query.CountAsync(cancellationToken);
		var items = await query
			.OrderByDescending(t => t.CreatedAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(t => new
			{
				t.Id,
				t.Title,
				descriptionPreview = t.Description.Length > 200 ? t.Description.Substring(0, 200) + "…" : t.Description,
				status = StatusString(t.Status),
				creatorId = t.CreatorUserId,
				creatorName = ((t.Creator.FirstName ?? "") + " " + (t.Creator.LastName ?? "")).Trim(),
				likesCount = t.Likes.Count,
				commentsCount = t.Comments.Count,
				isLikedByMe = t.Likes.Any(l => l.UserId == UserId),
				isAuthor = t.CreatorUserId == UserId,
				t.CreatedAt,
				canInteract = t.Status == FaceWallTicketStatus.Active && !isHost,
				isHostViewer = isHost,
			})
			.ToListAsync(cancellationToken);

		return Ok(new
		{
			items,
			isHostViewer = isHost,
			page,
			pageSize,
			totalCount = total,
			totalPages = (int)Math.Ceiling(total / (double)pageSize),
		});
	}

	[HttpGet("{ticketId:int}")]
	public async Task<IActionResult> Get(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new { error = "Face not found" });

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		var ticket = await _context.FaceWallTickets
			.AsNoTracking()
			.Include(t => t.Creator)
			.Include(t => t.Comments)
			.ThenInclude(c => c.User)
			.Include(t => t.Likes)
			.FirstOrDefaultAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);

		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		var comments = ticket.Comments
			.OrderBy(c => c.CreatedAt)
			.Select(c => new
			{
				c.Id,
				c.Content,
				userId = c.UserId,
				authorName = ((c.User.FirstName ?? "") + " " + (c.User.LastName ?? "")).Trim(),
				c.CreatedAt,
			})
			.ToList();

		return Ok(new
		{
			ticket.Id,
			ticket.Title,
			ticket.Description,
			status = StatusString(ticket.Status),
			creatorId = ticket.CreatorUserId,
			creatorName = ((ticket.Creator.FirstName ?? "") + " " + (ticket.Creator.LastName ?? "")).Trim(),
			likesCount = ticket.Likes.Count,
			commentsCount = ticket.Comments.Count,
			isLikedByMe = ticket.Likes.Any(l => l.UserId == UserId),
			isAuthor = ticket.CreatorUserId == UserId,
			ticket.CreatedAt,
			ticket.UpdatedAt,
			canInteract = ticket.Status == FaceWallTicketStatus.Active && !isHost,
			interactionsFrozen = IsFrozen(ticket.Status),
			isHostViewer = isHost,
			comments,
		});
	}

	[HttpPost]
	public async Task<IActionResult> Create(int faceId, [FromBody] WallTicketWriteDto dto, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new { error = "Host cannot create wall tickets" });

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new { error = "Face not found" });

		var title = dto.Title.Trim();
		var description = dto.Description.Trim();

		var count = await _context.FaceWallTickets.CountAsync(
			t => t.FaceId == faceId && t.CreatorUserId == UserId,
			cancellationToken);
		if (count >= MaxTicketsPerUserPerFace)
			return BadRequest(new { error = $"Maximum {MaxTicketsPerUserPerFace} wall tickets per user per face" });

		var ticket = new FaceWallTicket
		{
			FaceId = faceId,
			CreatorUserId = UserId,
			Title = title,
			Description = description,
			Status = FaceWallTicketStatus.Active,
			CreatedAt = DateTime.UtcNow,
		};
		_context.FaceWallTickets.Add(ticket);
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("Wall ticket {TicketId} created on face {FaceId} by {UserId}", ticket.Id, faceId, UserId);

		return CreatedAtAction(nameof(Get), new { faceId, ticketId = ticket.Id }, new
		{
			ticket.Id,
			ticket.Title,
			status = StatusString(ticket.Status),
			ticket.CreatedAt,
		});
	}

	[HttpPut("{ticketId:int}")]
	public async Task<IActionResult> Update(int faceId, int ticketId, [FromBody] WallTicketWriteDto dto, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		if (ticket.CreatorUserId != UserId)
			return Forbid();

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new { error = "Only active tickets can be edited" });

		if (dto.Title is not null)
			ticket.Title = dto.Title.Trim();

		if (dto.Description is not null)
			ticket.Description = dto.Description.Trim();

		ticket.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { ticket.Id, ticket.Title, status = StatusString(ticket.Status), ticket.UpdatedAt });
	}

	[HttpDelete("{ticketId:int}")]
	public async Task<IActionResult> Delete(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
		if (user == null)
			return Unauthorized();

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		var isAdmin = await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken);
		if (isAdmin)
		{
			_context.FaceWallTickets.Remove(ticket);
			await _context.SaveChangesAsync(cancellationToken);
			return NoContent();
		}

		if (ticket.CreatorUserId != UserId)
			return Forbid();

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new { error = "Only active tickets can be deleted by the author" });

		_context.FaceWallTickets.Remove(ticket);
		await _context.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	[HttpPost("{ticketId:int}/like")]
	public async Task<IActionResult> Like(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new { error = "Host cannot like wall tickets" });

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new { error = "Likes are frozen for this ticket" });

		if (await _context.FaceWallTicketLikes.AnyAsync(
				l => l.FaceWallTicketId == ticketId && l.UserId == UserId,
				cancellationToken))
			return Ok(new { liked = true });

		_context.FaceWallTicketLikes.Add(new FaceWallTicketLike
		{
			FaceWallTicketId = ticketId,
			UserId = UserId,
			CreatedAt = DateTime.UtcNow,
		});
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { liked = true });
	}

	[HttpDelete("{ticketId:int}/like")]
	public async Task<IActionResult> Unlike(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new { error = "Host cannot unlike wall tickets" });

		var ticket = await _context.FaceWallTickets.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);
		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new { error = "Likes are frozen for this ticket" });

		var like = await _context.FaceWallTicketLikes.FirstOrDefaultAsync(
			l => l.FaceWallTicketId == ticketId && l.UserId == UserId,
			cancellationToken);
		if (like == null)
			return Ok(new { liked = false });

		_context.FaceWallTicketLikes.Remove(like);
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { liked = false });
	}

	[HttpGet("{ticketId:int}/comments")]
	public async Task<IActionResult> ListComments(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var exists = await _context.FaceWallTickets.AsNoTracking()
			.AnyAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);
		if (!exists)
			return NotFound(new { error = "Ticket not found" });

		var list = await _context.FaceWallTicketComments
			.AsNoTracking()
			.Include(c => c.User)
			.Where(c => c.FaceWallTicketId == ticketId)
			.OrderBy(c => c.CreatedAt)
			.Select(c => new
			{
				c.Id,
				c.Content,
				userId = c.UserId,
				authorName = ((c.User.FirstName ?? "") + " " + (c.User.LastName ?? "")).Trim(),
				c.CreatedAt,
			})
			.ToListAsync(cancellationToken);

		return Ok(list);
	}

	[HttpPost("{ticketId:int}/comments")]
	public async Task<IActionResult> AddComment(
		int faceId,
		int ticketId,
		[FromBody] WallTicketCommentDto dto,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new { error = "Host cannot comment on wall tickets" });

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new { error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new { error = "Comments are frozen for this ticket" });

		var comment = new FaceWallTicketComment
		{
			FaceWallTicketId = ticketId,
			UserId = UserId,
			Content = dto.Content.Trim(),
			CreatedAt = DateTime.UtcNow,
		};
		_context.FaceWallTicketComments.Add(comment);
		await _context.SaveChangesAsync(cancellationToken);

		var author = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == UserId, cancellationToken);
		return CreatedAtAction(nameof(ListComments), new { faceId, ticketId }, new
		{
			comment.Id,
			comment.Content,
			userId = comment.UserId,
			authorName = ((author.FirstName ?? "") + " " + (author.LastName ?? "")).Trim(),
			comment.CreatedAt,
		});
	}

}
