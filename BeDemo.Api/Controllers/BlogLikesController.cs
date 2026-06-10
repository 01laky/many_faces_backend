using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/blogs/{blogId}/likes")]
[Authorize]
public class BlogLikesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<BlogLikesController> _logger;

	public BlogLikesController(
		ApplicationDbContext context,
		ILogger<BlogLikesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/blogs/{blogId}/likes - Get likes for blog</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ContentLikeItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLikes(int blogId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new ErrorResponseDto { Error = "Blog not found" });

		var likes = await _context.BlogLikes
			.Where(l => l.BlogId == blogId)
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

	/// <summary>POST /api/blogs/{blogId}/likes - Like blog</summary>
	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> LikeBlog(int blogId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new ErrorResponseDto { Error = "Blog not found" });

		var exists = await _context.BlogLikes
			.AnyAsync(l => l.BlogId == blogId && l.UserId == UserId);

		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Already liked" });

		_context.BlogLikes.Add(new BlogLike
		{
			BlogId = blogId,
			UserId = UserId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} liked blog {BlogId}", UserId, blogId);
		return Ok(SuccessResultDto.True);
	}

	/// <summary>DELETE /api/blogs/{blogId}/likes - Unlike blog</summary>
	[HttpDelete]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UnlikeBlog(int blogId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var like = await _context.BlogLikes
			.FirstOrDefaultAsync(l => l.BlogId == blogId && l.UserId == UserId);

		if (like == null)
			return NotFound(new ErrorResponseDto { Error = "Like not found" });

		_context.BlogLikes.Remove(like);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} unliked blog {BlogId}", UserId, blogId);
		return Ok(SuccessResultDto.True);
	}
}
