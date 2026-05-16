using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Reels;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/reels/{reelId:int}/comments")]
[Authorize]
public class ReelCommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReelCommentsController> _logger;

    public ReelCommentsController(ApplicationDbContext context, ILogger<ReelCommentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetComments(int reelId, [FromQuery] ReelCommentCreateQuery commentQuery)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var faceId = commentQuery.FaceId;
        var reel = await _context.Reels
            .Include(r => r.ReelFaces)
            .FirstOrDefaultAsync(r => r.Id == reelId);

        if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
            return NotFound(new { error = "Reel not found" });

        var comments = await _context.ReelComments
            .Where(c => c.ReelId == reelId)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.ReelId,
                c.UserId,
                userName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
                c.Content,
                c.CreatedAt,
                c.UpdatedAt,
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost]
    public async Task<IActionResult> CreateComment(int reelId, [FromQuery] ReelCommentCreateQuery commentQuery, [FromBody] CreateReelCommentDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var faceId = commentQuery.FaceId;
        var reel = await _context.Reels
            .Include(r => r.ReelFaces)
            .FirstOrDefaultAsync(r => r.Id == reelId);

        if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
            return NotFound(new { error = "Reel not found" });

        var comment = new ReelComment
        {
            ReelId = reelId,
            UserId = UserId,
            Content = dto.Content.Trim(),
        };

        _context.ReelComments.Add(comment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} commented on reel {ReelId}", UserId, reelId);
        return CreatedAtAction(nameof(GetComments), new { reelId }, new
        {
            comment.Id,
            comment.ReelId,
            comment.UserId,
            comment.Content,
            comment.CreatedAt,
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateComment(int reelId, int id, [FromBody] UpdateReelCommentDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var comment = await _context.ReelComments
            .FirstOrDefaultAsync(c => c.Id == id && c.ReelId == reelId);

        if (comment == null)
            return NotFound(new { error = "Comment not found" });

        if (comment.UserId != UserId)
            return Forbid();

        comment.Content = dto.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            comment.Id,
            comment.ReelId,
            comment.UserId,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteComment(int reelId, int id)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var comment = await _context.ReelComments
            .FirstOrDefaultAsync(c => c.Id == id && c.ReelId == reelId);

        if (comment == null)
            return NotFound(new { error = "Comment not found" });

        if (comment.UserId != UserId)
            return Forbid();

        _context.ReelComments.Remove(comment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
