using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/reels/{reelId:int}/likes")]
[Authorize]
public class ReelLikesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<ReelLikesController> _logger;

	public ReelLikesController(ApplicationDbContext context, ILogger<ReelLikesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ContentLikeItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLikes(int reelId, [FromQuery] int? faceId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var likes = await _context.ReelLikes
			.Where(l => l.ReelId == reelId)
			.Include(l => l.User)
			.OrderByDescending(l => l.CreatedAt)
			.Select(l => new ContentLikeItemDto
			{
				Id = l.Id,
				UserId = l.UserId,
				UserName = (l.User.FirstName ?? "") + " " + (l.User.LastName ?? ""),
				CreatedAt = l.CreatedAt,
			})
			.ToListAsync();

		return Ok(likes);
	}

	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> LikeReel(int reelId, [FromQuery] int? faceId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var exists = await _context.ReelLikes
			.AnyAsync(l => l.ReelId == reelId && l.UserId == UserId);

		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Already liked" });

		_context.ReelLikes.Add(new ReelLike
		{
			ReelId = reelId,
			UserId = UserId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} liked reel {ReelId}", UserId, reelId);
		return Ok(SuccessResultDto.True);
	}

	[HttpDelete]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UnlikeReel(int reelId, [FromQuery] int? faceId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var reel = await _context.Reels
			.Include(r => r.ReelFaces)
			.FirstOrDefaultAsync(r => r.Id == reelId);

		if (reel == null || !ReelVisibility.IsVisibleForFace(reel, faceId))
			return NotFound(new ErrorResponseDto { Error = "Reel not found" });

		var like = await _context.ReelLikes
			.FirstOrDefaultAsync(l => l.ReelId == reelId && l.UserId == UserId);

		if (like == null)
			return NotFound(new ErrorResponseDto { Error = "Like not found" });

		_context.ReelLikes.Remove(like);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} unliked reel {ReelId}", UserId, reelId);
		return Ok(SuccessResultDto.True);
	}
}
