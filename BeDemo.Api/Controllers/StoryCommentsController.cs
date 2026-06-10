using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Stories;
using BeDemo.Api.Utils;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/stories/{storyId:int}/comments")]
[Authorize]
public class StoryCommentsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<StoryCommentsController> _logger;

	public StoryCommentsController(ApplicationDbContext context, ILogger<StoryCommentsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<StoryCommentListItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComments(int storyId, [FromQuery] StoryScopedQuery scopedQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, scopedQuery.FaceId, UserId, cancellationToken);
		if (story == null)
			return NotFound(new ErrorResponseDto { Error = "Story not found" });

		var comments = await _context.StoryComments
			.Where(c => c.StoryId == storyId)
			.Include(c => c.User)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new StoryCommentListItemDto
			{
				Id = c.Id,
				UserId = c.UserId,
				UserName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
				Content = c.Content,
				CreatedAt = c.CreatedAt,
			})
			.ToListAsync(cancellationToken);

		return Ok(comments);
	}

	[HttpPost]
	[ProducesResponseType(typeof(CreatedEntityDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> CreateComment(
		int storyId,
		[FromQuery] StoryScopedQuery scopedQuery,
		[FromBody] CreateStoryCommentDto dto,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, scopedQuery.FaceId, UserId, cancellationToken);
		if (story == null)
			return NotFound(new ErrorResponseDto { Error = "Story not found" });

		var comment = new StoryComment
		{
			StoryId = storyId,
			UserId = UserId,
			Content = dto.Content.Trim(),
		};
		_context.StoryComments.Add(comment);
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("User {UserId} commented on story {StoryId}", UserId, storyId);
		return Ok(new CreatedEntityDto { Id = comment.Id });
	}
}
