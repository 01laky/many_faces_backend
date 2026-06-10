using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/albums/{albumId}/likes")]
[Authorize]
public class AlbumLikesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<AlbumLikesController> _logger;

	public AlbumLikesController(
		ApplicationDbContext context,
		ILogger<AlbumLikesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/albums/{albumId}/likes - Get likes for album</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ContentLikeItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLikes(int albumId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums.FindAsync(albumId);
		if (album == null)
			return NotFound(new ErrorResponseDto { Error = "Album not found" });

		if (album.AlbumType != AlbumTypeEnum.Public && album.CreatorId != UserId)
			return Forbid();

		var likes = await _context.AlbumLikes
			.Where(l => l.AlbumId == albumId)
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

	/// <summary>POST /api/albums/{albumId}/likes - Like album</summary>
	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> LikeAlbum(int albumId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var album = await _context.Albums.FindAsync(albumId);
		if (album == null)
			return NotFound(new ErrorResponseDto { Error = "Album not found" });

		if (album.AlbumType != AlbumTypeEnum.Public && album.CreatorId != UserId)
			return Forbid();

		var exists = await _context.AlbumLikes
			.AnyAsync(l => l.AlbumId == albumId && l.UserId == UserId);

		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Already liked" });

		_context.AlbumLikes.Add(new AlbumLike
		{
			AlbumId = albumId,
			UserId = UserId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} liked album {AlbumId}", UserId, albumId);
		return Ok(SuccessResultDto.True);
	}

	/// <summary>DELETE /api/albums/{albumId}/likes - Unlike album</summary>
	[HttpDelete]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UnlikeAlbum(int albumId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var like = await _context.AlbumLikes
			.FirstOrDefaultAsync(l => l.AlbumId == albumId && l.UserId == UserId);

		if (like == null)
			return NotFound(new ErrorResponseDto { Error = "Like not found" });

		_context.AlbumLikes.Remove(like);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} unliked album {AlbumId}", UserId, albumId);
		return Ok(SuccessResultDto.True);
	}
}
