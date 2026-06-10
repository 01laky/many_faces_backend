using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

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
	public async Task<IActionResult> GetComments(int blogId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new { error = "Blog not found" });

		var comments = await _context.BlogComments
			.Where(c => c.BlogId == blogId)
			.Include(c => c.User)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new
			{
				c.Id,
				c.BlogId,
				c.UserId,
				userName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
				c.Content,
				c.CreatedAt,
				c.UpdatedAt,
			})
			.ToListAsync();

		return Ok(comments);
	}

	/// <summary>POST /api/blogs/{blogId}/comments - Add comment</summary>
	[HttpPost]
	public async Task<IActionResult> CreateComment(int blogId, [FromBody] CreateBlogCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blog = await _context.Blogs.FindAsync(blogId);
		if (blog == null)
			return NotFound(new { error = "Blog not found" });

		var comment = new BlogComment
		{
			BlogId = blogId,
			UserId = UserId,
			Content = dto.Content.Trim(),
		};

		_context.BlogComments.Add(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} commented on blog {BlogId}", UserId, blogId);
		return CreatedAtAction(nameof(GetComments), new { blogId }, new
		{
			comment.Id,
			comment.BlogId,
			comment.UserId,
			comment.Content,
			comment.CreatedAt,
		});
	}

	/// <summary>PUT /api/blogs/{blogId}/comments/{id} - Update comment (author only)</summary>
	[HttpPut("{id}")]
	public async Task<IActionResult> UpdateComment(int blogId, int id, [FromBody] UpdateBlogCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.BlogComments
			.FirstOrDefaultAsync(c => c.Id == id && c.BlogId == blogId);

		if (comment == null)
			return NotFound(new { error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		comment.Content = dto.Content.Trim();
		comment.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} updated comment {CommentId}", UserId, id);
		return Ok(new
		{
			comment.Id,
			comment.BlogId,
			comment.UserId,
			comment.Content,
			comment.CreatedAt,
			comment.UpdatedAt,
		});
	}

	/// <summary>DELETE /api/blogs/{blogId}/comments/{id} - Delete comment (author only)</summary>
	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteComment(int blogId, int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.BlogComments
			.FirstOrDefaultAsync(c => c.Id == id && c.BlogId == blogId);

		if (comment == null)
			return NotFound(new { error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		_context.BlogComments.Remove(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} deleted comment {CommentId}", UserId, id);
		return NoContent();
	}
}
