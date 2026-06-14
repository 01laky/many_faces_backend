using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Utils;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/faces/{faceId:int}/wall-tickets")]
[Authorize]
public class FaceWallTicketsController : ApiControllerBase
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
	[ProducesResponseType(typeof(WallTicketListEnvelopeDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(
		int faceId,
		[FromQuery] WallTicketListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		var query = _context.FaceWallTickets
			.AsNoTracking()
			.Include(t => t.Creator)
			.Where(t => t.FaceId == faceId);

		var total = await query.CountAsync(cancellationToken);
		// Clamp exactly like the grid-snapshot wall-tickets block (BE-RP34 contract parity): an
		// out-of-range page is pulled back in bounds, an empty list reports one (empty) page rather than
		// zero, and pageSize 0 cannot divide-by-zero. Previously this endpoint returned raw page +
		// Math.Ceiling(total / pageSize), which disagreed with the snapshot for an empty list (0 vs 1).
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, total);
		page = clampedPage;
		var rawItems = await query
			.OrderByDescending(t => t.CreatedAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(t => new
			{
				t.Id,
				t.Title,
				DescriptionPreview = t.Description.Length > 200 ? t.Description.Substring(0, 200) + "…" : t.Description,
				t.Status,
				CreatorId = t.CreatorUserId,
				CreatorName = ((t.Creator.FirstName ?? "") + " " + (t.Creator.LastName ?? "")).Trim(),
				LikesCount = t.Likes.Count,
				CommentsCount = t.Comments.Count,
				IsLikedByMe = t.Likes.Any(l => l.UserId == UserId),
				IsAuthor = t.CreatorUserId == UserId,
				t.CreatedAt,
				CanInteract = t.Status == FaceWallTicketStatus.Active && !isHost,
			})
			.ToListAsync(cancellationToken);

		var items = rawItems.Select(t => new WallTicketListItemDto
		{
			Id = t.Id,
			Title = t.Title,
			DescriptionPreview = t.DescriptionPreview,
			Status = StatusString(t.Status),
			CreatorId = t.CreatorId,
			CreatorName = t.CreatorName,
			LikesCount = t.LikesCount,
			CommentsCount = t.CommentsCount,
			IsLikedByMe = t.IsLikedByMe,
			IsAuthor = t.IsAuthor,
			CreatedAt = t.CreatedAt,
			CanInteract = t.CanInteract,
			IsHostViewer = isHost,
		});

		return Ok(new WallTicketListEnvelopeDto
		{
			Items = items,
			IsHostViewer = isHost,
			Page = page,
			PageSize = pageSize,
			TotalCount = total,
			TotalPages = totalPages,
		});
	}

	[HttpGet("{ticketId:int}")]
	[ProducesResponseType(typeof(WallTicketDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Get(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken);

		var ticket = await _context.FaceWallTickets
			.AsNoTracking()
			.Include(t => t.Creator)
			.Include(t => t.Comments)
			.ThenInclude(c => c.User)
			.Include(t => t.Likes)
			.FirstOrDefaultAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);

		if (ticket == null)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		var comments = ticket.Comments
			.OrderBy(c => c.CreatedAt)
			.Select(c => new WallTicketDetailCommentDto
			{
				Id = c.Id,
				Content = c.Content,
				UserId = c.UserId,
				AuthorName = ((c.User.FirstName ?? "") + " " + (c.User.LastName ?? "")).Trim(),
				CreatedAt = c.CreatedAt,
			})
			.ToList();

		return Ok(new WallTicketDetailDto
		{
			Id = ticket.Id,
			Title = ticket.Title,
			Description = ticket.Description,
			Status = StatusString(ticket.Status),
			CreatorId = ticket.CreatorUserId,
			CreatorName = ((ticket.Creator.FirstName ?? "") + " " + (ticket.Creator.LastName ?? "")).Trim(),
			LikesCount = ticket.Likes.Count,
			CommentsCount = ticket.Comments.Count,
			IsLikedByMe = ticket.Likes.Any(l => l.UserId == UserId),
			IsAuthor = ticket.CreatorUserId == UserId,
			CreatedAt = ticket.CreatedAt,
			UpdatedAt = ticket.UpdatedAt,
			CanInteract = ticket.Status == FaceWallTicketStatus.Active && !isHost,
			InteractionsFrozen = IsFrozen(ticket.Status),
			IsHostViewer = isHost,
			Comments = comments,
		});
	}

	[HttpPost]
	[ProducesResponseType(typeof(WallTicketCreatedDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create(int faceId, [FromBody] WallTicketWriteDto dto, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto { Error = "Host cannot create wall tickets" });

		var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var title = dto.Title.Trim();
		var description = dto.Description.Trim();

		var count = await _context.FaceWallTickets.CountAsync(
			t => t.FaceId == faceId && t.CreatorUserId == UserId,
			cancellationToken);
		if (count >= MaxTicketsPerUserPerFace)
			return BadRequest(new ErrorResponseDto { Error = $"Maximum {MaxTicketsPerUserPerFace} wall tickets per user per face" });

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

		return CreatedAtAction(nameof(Get), new { faceId, ticketId = ticket.Id }, new WallTicketCreatedDto
		{
			Id = ticket.Id,
			Title = ticket.Title,
			Status = StatusString(ticket.Status),
			CreatedAt = ticket.CreatedAt,
		});
	}

	[HttpPut("{ticketId:int}")]
	[ProducesResponseType(typeof(WallTicketUpdateResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(int faceId, int ticketId, [FromBody] WallTicketWriteDto dto, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		if (ticket.CreatorUserId != UserId)
			return Forbid();

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new ErrorResponseDto { Error = "Only active tickets can be edited" });

		if (dto.Title is not null)
			ticket.Title = dto.Title.Trim();

		if (dto.Description is not null)
			ticket.Description = dto.Description.Trim();

		ticket.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new WallTicketUpdateResultDto { Id = ticket.Id, Title = ticket.Title, Status = StatusString(ticket.Status), UpdatedAt = ticket.UpdatedAt });
	}

	[HttpDelete("{ticketId:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
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
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

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
			return BadRequest(new ErrorResponseDto { Error = "Only active tickets can be deleted by the author" });

		_context.FaceWallTickets.Remove(ticket);
		await _context.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	[HttpPost("{ticketId:int}/like")]
	[ProducesResponseType(typeof(LikeResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Like(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto { Error = "Host cannot like wall tickets" });

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new ErrorResponseDto { Error = "Likes are frozen for this ticket" });

		if (await _context.FaceWallTicketLikes.AnyAsync(
				l => l.FaceWallTicketId == ticketId && l.UserId == UserId,
				cancellationToken))
			return Ok(LikeResultDto.Yes);

		_context.FaceWallTicketLikes.Add(new FaceWallTicketLike
		{
			FaceWallTicketId = ticketId,
			UserId = UserId,
			CreatedAt = DateTime.UtcNow,
		});
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(LikeResultDto.Yes);
	}

	[HttpDelete("{ticketId:int}/like")]
	[ProducesResponseType(typeof(LikeResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Unlike(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto { Error = "Host cannot unlike wall tickets" });

		var ticket = await _context.FaceWallTickets.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);
		if (ticket == null)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new ErrorResponseDto { Error = "Likes are frozen for this ticket" });

		var like = await _context.FaceWallTicketLikes.FirstOrDefaultAsync(
			l => l.FaceWallTicketId == ticketId && l.UserId == UserId,
			cancellationToken);
		if (like == null)
			return Ok(LikeResultDto.No);

		_context.FaceWallTicketLikes.Remove(like);
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(LikeResultDto.No);
	}

	[HttpGet("{ticketId:int}/comments")]
	[ProducesResponseType(typeof(IReadOnlyList<WallTicketCommentResultDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListComments(int faceId, int ticketId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var exists = await _context.FaceWallTickets.AsNoTracking()
			.AnyAsync(t => t.Id == ticketId && t.FaceId == faceId, cancellationToken);
		if (!exists)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		var list = await _context.FaceWallTicketComments
			.AsNoTracking()
			.Include(c => c.User)
			.Where(c => c.FaceWallTicketId == ticketId)
			.OrderBy(c => c.CreatedAt)
			.Select(c => new WallTicketCommentResultDto
			{
				Id = c.Id,
				Content = c.Content,
				UserId = c.UserId,
				AuthorName = ((c.User.FirstName ?? "") + " " + (c.User.LastName ?? "")).Trim(),
				CreatedAt = c.CreatedAt,
			})
			.ToListAsync(cancellationToken);

		return Ok(list);
	}

	[HttpPost("{ticketId:int}/comments")]
	[ProducesResponseType(typeof(WallTicketCommentResultDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> AddComment(
		int faceId,
		int ticketId,
		[FromBody] WallTicketCommentDto dto,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, faceId, cancellationToken))
			return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto { Error = "Host cannot comment on wall tickets" });

		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
			t => t.Id == ticketId && t.FaceId == faceId,
			cancellationToken);
		if (ticket == null)
			return NotFound(new ErrorResponseDto { Error = "Ticket not found" });

		if (ticket.Status != FaceWallTicketStatus.Active)
			return BadRequest(new ErrorResponseDto { Error = "Comments are frozen for this ticket" });

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
		return CreatedAtAction(nameof(ListComments), new { faceId, ticketId }, new WallTicketCommentResultDto
		{
			Id = comment.Id,
			Content = comment.Content,
			UserId = comment.UserId,
			AuthorName = ((author.FirstName ?? "") + " " + (author.LastName ?? "")).Trim(),
			CreatedAt = comment.CreatedAt,
		});
	}

}
