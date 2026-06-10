using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/blogs/{blogId}/comments")]
[Authorize]
public class BlogCommentsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<BlogCommentsController> _logger;

	public BlogCommentsController(
		ApplicationDbContext context,
		ILogger<BlogCommentsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/blogs/{blogId}/comments - Get comments for blog</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<BlogCommentListItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComments(int blogId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new ErrorResponseDto { Error = "Blog not found" });

		var comments = await _context.BlogComments
			.Where(c => c.BlogId == blogId)
			.Include(c => c.User)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new BlogCommentListItemDto
			{
				Id = c.Id,
				BlogId = c.BlogId,
				UserId = c.UserId,
				UserName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
				Content = c.Content,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt,
			})
			.ToListAsync();

		return Ok(comments);
	}

	/// <summary>POST /api/blogs/{blogId}/comments - Add comment</summary>
	[HttpPost]
	[ProducesResponseType(typeof(BlogCommentDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateComment(int blogId, [FromBody] CreateBlogCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new ErrorResponseDto { Error = "Blog not found" });

		var comment = new BlogComment
		{
			BlogId = blogId,
			UserId = UserId,
			Content = dto.Content.Trim(),
		};

		_context.BlogComments.Add(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} commented on blog {BlogId}", UserId, blogId);
		return CreatedAtAction(nameof(GetComments), new { blogId }, new BlogCommentDto
		{
			Id = comment.Id,
			BlogId = comment.BlogId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
		});
	}

	/// <summary>PUT /api/blogs/{blogId}/comments/{id} - Update comment (author only)</summary>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(BlogCommentDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateComment(int blogId, int id, [FromBody] UpdateBlogCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.BlogComments
			.FirstOrDefaultAsync(c => c.Id == id && c.BlogId == blogId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		comment.Content = dto.Content.Trim();
		comment.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} updated comment {CommentId}", UserId, id);
		return Ok(new BlogCommentDto
		{
			Id = comment.Id,
			BlogId = comment.BlogId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
			UpdatedAt = comment.UpdatedAt,
		});
	}

	/// <summary>DELETE /api/blogs/{blogId}/comments/{id} - Delete comment (author only)</summary>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteComment(int blogId, int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.BlogComments
			.FirstOrDefaultAsync(c => c.Id == id && c.BlogId == blogId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		_context.BlogComments.Remove(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} deleted comment {CommentId}", UserId, id);
		return NoContent();
	}
}
