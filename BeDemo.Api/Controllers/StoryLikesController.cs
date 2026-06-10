using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/stories/{storyId:int}/likes")]
[Authorize]
public class StoryLikesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<StoryLikesController> _logger;

	public StoryLikesController(ApplicationDbContext context, ILogger<StoryLikesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ContentLikeItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLikes(int storyId, [FromQuery] int faceId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, faceId, UserId, cancellationToken);
		if (story == null)
			return NotFound(new ErrorResponseDto { Error = "Story not found" });

		var likes = await _context.StoryLikes
			.Where(l => l.StoryId == storyId)
			.Include(l => l.User)
			.OrderByDescending(l => l.CreatedAt)
			.Select(l => new ContentLikeItemDto
			{
				Id = l.Id,
				UserId = l.UserId,
				UserName = (l.User.FirstName ?? "") + " " + (l.User.LastName ?? ""),
				CreatedAt = l.CreatedAt,
			})
			.ToListAsync(cancellationToken);

		return Ok(likes);
	}

	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Like(int storyId, [FromQuery] int faceId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, faceId, UserId, cancellationToken);
		if (story == null)
			return NotFound(new ErrorResponseDto { Error = "Story not found" });

		var exists = await _context.StoryLikes.AnyAsync(
			l => l.StoryId == storyId && l.UserId == UserId,
			cancellationToken);
		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Already liked" });

		_context.StoryLikes.Add(new StoryLike { StoryId = storyId, UserId = UserId });
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("User {UserId} liked story {StoryId}", UserId, storyId);
		return Ok(SuccessResultDto.True);
	}

	[HttpDelete]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Unlike(int storyId, [FromQuery] int faceId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, faceId, UserId, cancellationToken);
		if (story == null)
			return NotFound(new ErrorResponseDto { Error = "Story not found" });

		var like = await _context.StoryLikes.FirstOrDefaultAsync(
			l => l.StoryId == storyId && l.UserId == UserId,
			cancellationToken);
		if (like == null)
			return Ok(SuccessResultDto.False);

		_context.StoryLikes.Remove(like);
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(SuccessResultDto.True);
	}
}
