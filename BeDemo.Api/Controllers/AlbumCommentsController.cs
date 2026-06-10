using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/albums/{albumId}/comments")]
[Authorize]
public class AlbumCommentsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<AlbumCommentsController> _logger;

	public AlbumCommentsController(
		ApplicationDbContext context,
		ILogger<AlbumCommentsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/albums/{albumId}/comments - Get comments for album</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<AlbumCommentListItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComments(int albumId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums.FindAsync(albumId);
		if (album == null)
			return NotFound(new ErrorResponseDto { Error = "Album not found" });

		// Visibility check
		if (album.AlbumType != AlbumTypeEnum.Public && album.CreatorId != UserId)
			return Forbid();

		var comments = await _context.AlbumComments
			.Where(c => c.AlbumId == albumId)
			.Include(c => c.User)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new AlbumCommentListItemDto
			{
				Id = c.Id,
				AlbumId = c.AlbumId,
				UserId = c.UserId,
				UserName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
				Content = c.Content,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt,
			})
			.ToListAsync();

		return Ok(comments);
	}

	/// <summary>POST /api/albums/{albumId}/comments - Add comment</summary>
	[HttpPost]
	[ProducesResponseType(typeof(AlbumCommentDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateComment(int albumId, [FromBody] CreateAlbumCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums.FindAsync(albumId);
		if (album == null)
			return NotFound(new ErrorResponseDto { Error = "Album not found" });

		if (album.AlbumType != AlbumTypeEnum.Public && album.CreatorId != UserId)
			return Forbid();

		var comment = new AlbumComment
		{
			AlbumId = albumId,
			UserId = UserId,
			Content = dto.Content.Trim(),
		};

		_context.AlbumComments.Add(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} commented on album {AlbumId}", UserId, albumId);
		return CreatedAtAction(nameof(GetComments), new { albumId }, new AlbumCommentDto
		{
			Id = comment.Id,
			AlbumId = comment.AlbumId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
		});
	}

	/// <summary>PUT /api/albums/{albumId}/comments/{id} - Update comment (author only)</summary>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(AlbumCommentDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateComment(int albumId, int id, [FromBody] UpdateAlbumCommentDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.AlbumComments
			.FirstOrDefaultAsync(c => c.Id == id && c.AlbumId == albumId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		comment.Content = dto.Content.Trim();
		comment.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} updated comment {CommentId}", UserId, id);
		return Ok(new AlbumCommentDto
		{
			Id = comment.Id,
			AlbumId = comment.AlbumId,
			UserId = comment.UserId,
			Content = comment.Content,
			CreatedAt = comment.CreatedAt,
			UpdatedAt = comment.UpdatedAt,
		});
	}

	/// <summary>DELETE /api/albums/{albumId}/comments/{id} - Delete comment (author only)</summary>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteComment(int albumId, int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var comment = await _context.AlbumComments
			.FirstOrDefaultAsync(c => c.Id == id && c.AlbumId == albumId);

		if (comment == null)
			return NotFound(new ErrorResponseDto { Error = "Comment not found" });

		if (comment.UserId != UserId)
			return Forbid();

		_context.AlbumComments.Remove(comment);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} deleted comment {CommentId}", UserId, id);
		return NoContent();
	}
}
