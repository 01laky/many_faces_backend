using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/admin/faces/{faceId:int}/wall-tickets")]
[Authorize]
public class AdminFaceWallTicketsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFaceWallTicketLifecycleService _lifecycle;
    private readonly ILogger<AdminFaceWallTicketsController> _logger;

    public AdminFaceWallTicketsController(
        ApplicationDbContext context,
        IFaceWallTicketLifecycleService lifecycle,
        ILogger<AdminFaceWallTicketsController> logger)
    {
        _context = context;
        _lifecycle = lifecycle;
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

    private async Task<IActionResult?> RequireGlobalAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
        if (user == null)
            return Unauthorized();
        if (!await FaceChatRoomAuth.IsGlobalAdminAsync(_context, user, cancellationToken))
            return Forbid();
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        int faceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

        var faceExists = await _context.Faces.AsNoTracking().AnyAsync(f => f.Id == faceId, cancellationToken);
        if (!faceExists)
            return NotFound(new { error = "Face not found" });

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

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
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalCount = total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
        });
    }

    [HttpGet("{ticketId:int}")]
    public async Task<IActionResult> Get(int faceId, int ticketId, CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

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
            ticket.CreatedAt,
            ticket.UpdatedAt,
            comments,
        });
    }

    [HttpPost("{ticketId:int}/approve")]
    public async Task<IActionResult> Approve(int faceId, int ticketId, CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

        var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.FaceId == faceId,
            cancellationToken);
        if (ticket == null)
            return NotFound(new { error = "Ticket not found" });

        if (ticket.Status != FaceWallTicketStatus.Active)
            return BadRequest(new { error = "Only active tickets can be approved" });

        ticket.Status = FaceWallTicketStatus.Approved;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Wall ticket {TicketId} approved by admin {UserId}", ticketId, UserId);
        return Ok(new { ticket.Id, status = StatusString(ticket.Status) });
    }

    [HttpPost("{ticketId:int}/deny")]
    public async Task<IActionResult> Deny(int faceId, int ticketId, CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

        var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.FaceId == faceId,
            cancellationToken);
        if (ticket == null)
            return NotFound(new { error = "Ticket not found" });

        if (ticket.Status != FaceWallTicketStatus.Active)
            return BadRequest(new { error = "Only active tickets can be denied" });

        ticket.Status = FaceWallTicketStatus.Denied;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _lifecycle.ScheduleDeniedTicketDeletionAsync(ticketId, cancellationToken);
        _logger.LogInformation("Wall ticket {TicketId} denied by admin {UserId}", ticketId, UserId);
        return Ok(new { ticket.Id, status = StatusString(ticket.Status) });
    }

    [HttpDelete("{ticketId:int}")]
    public async Task<IActionResult> DeleteTicket(int faceId, int ticketId, CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

        var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.FaceId == faceId,
            cancellationToken);
        if (ticket == null)
            return NotFound(new { error = "Ticket not found" });

        _context.FaceWallTickets.Remove(ticket);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{ticketId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(
        int faceId,
        int ticketId,
        int commentId,
        CancellationToken cancellationToken = default)
    {
        var denied = await RequireGlobalAdminAsync(cancellationToken);
        if (denied != null)
            return denied;

        var comment = await _context.FaceWallTicketComments
            .Include(c => c.Ticket)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.FaceWallTicketId == ticketId, cancellationToken);
        if (comment == null)
            return NotFound(new { error = "Comment not found" });

        if (comment.Ticket.FaceId != faceId)
            return NotFound(new { error = "Comment not found" });

        _context.FaceWallTicketComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
