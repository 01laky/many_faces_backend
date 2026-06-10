using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.Reels;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/reels/{reelId:int}/comments")]
[Authorize]
public class ReelCommentsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<ReelCommentsController> _logger;

	public ReelCommentsController(ApplicationDbContext context, ILogger<ReelCommentsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ReelCommentListItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComments(int reelId, [FromQuery] ReelCommentCreateQuery commentQuery)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = commentQuery.FaceId;
		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var comments = await _context.ReelComments
			.Where(c => c.ReelId == reelId)
			.Include(c => c.User)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new ReelCommentListItemDto
			{
				Id = c.Id,
				ReelId = c.ReelId,
				UserId = c.UserId,
				UserName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
				Content = c.Content,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt,
			})
			.ToListAsync();

		return Ok(comments);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ReelCommentDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateComment(int reelId, [FromQuery] ReelCommentCreateQuery commentQuery, [FromBody] CreateReelCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceId = commentQuery.FaceId;
		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var comment = new ReelComment
		{
			ReelId = reelId,
			UserId = UserId,
			Content = dto.Content.Trim(),
		};

		_context.ReelComments.Add(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} commented on reel {ReelId}", UserId, reelId);
		return CreatedAtAction(nameof(GetComments), new { reelId }, new ReelCommentDto
		{
			Id = comment.Id,
			ReelId = comment.ReelId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
		});
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(ReelCommentDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateComment(int reelId, int id, [FromBody] UpdateReelCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.ReelComments
			.FirstOrDefaultAsync(c => c.Id == id && c.ReelId == reelId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		comment.Content = dto.Content.Trim();
		comment.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		return Ok(new ReelCommentDto
		{
			Id = comment.Id,
			ReelId = comment.ReelId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
			UpdatedAt = comment.UpdatedAt,
		});
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteComment(int reelId, int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.ReelComments
			.FirstOrDefaultAsync(c => c.Id == id && c.ReelId == reelId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		_context.ReelComments.Remove(comment);
		await _context.SaveChangesAsync();

		return NoContent();
	}
}
