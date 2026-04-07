using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AlbumsController> _logger;

    public AlbumsController(
        ApplicationDbContext context,
        ILogger<AlbumsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>GET /api/albums - Get all visible albums (public + own private/paid)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAlbums()
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var albums = await _context.Albums
            .Where(a => a.AlbumType == AlbumTypeEnum.Public || a.CreatorId == UserId)
            .Include(a => a.Creator)
            .Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
            .Include(a => a.Likes)
            .Include(a => a.Comments)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                albumType = (int)a.AlbumType,
                mediaType = (int)a.MediaType,
                creatorId = a.CreatorId,
                creatorName = (a.Creator.FirstName ?? "") + " " + (a.Creator.LastName ?? ""),
                faces = a.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
                likesCount = a.Likes.Count,
                commentsCount = a.Comments.Count,
                a.CreatedAt,
                a.UpdatedAt,
            })
            .ToListAsync();

        return Ok(albums);
    }

    /// <summary>GET /api/albums/{id} - Get album by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var album = await _context.Albums
            .Include(a => a.Creator)
            .Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
            .Include(a => a.Likes)
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (album == null)
            return NotFound(new { error = "Album not found" });

        // Visibility check: private/paid only visible to creator
        if (album.AlbumType != AlbumTypeEnum.Public && album.CreatorId != UserId)
            return Forbid();

        return Ok(new
        {
            album.Id,
            album.Title,
            album.Description,
            albumType = (int)album.AlbumType,
            mediaType = (int)album.MediaType,
            creatorId = album.CreatorId,
            creatorName = (album.Creator.FirstName ?? "") + " " + (album.Creator.LastName ?? ""),
            faces = album.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
            likesCount = album.Likes.Count,
            commentsCount = album.Comments.Count,
            isLikedByMe = album.Likes.Any(l => l.UserId == UserId),
            album.CreatedAt,
            album.UpdatedAt,
        });
    }

    /// <summary>GET /api/albums/user/{userId} - Get albums by user</summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetAlbumsByUser(string userId)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var query = _context.Albums.Where(a => a.CreatorId == userId);

        // Other users can only see public albums
        if (userId != UserId)
            query = query.Where(a => a.AlbumType == AlbumTypeEnum.Public);

        var albums = await query
            .Include(a => a.Creator)
            .Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
            .Include(a => a.Likes)
            .Include(a => a.Comments)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                albumType = (int)a.AlbumType,
                mediaType = (int)a.MediaType,
                creatorId = a.CreatorId,
                creatorName = (a.Creator.FirstName ?? "") + " " + (a.Creator.LastName ?? ""),
                faces = a.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
                likesCount = a.Likes.Count,
                commentsCount = a.Comments.Count,
                a.CreatedAt,
                a.UpdatedAt,
            })
            .ToListAsync();

        return Ok(albums);
    }

    /// <summary>POST /api/albums - Create album</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "Title is required" });

        var album = new Album
        {
            CreatorId = UserId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            AlbumType = dto.AlbumType,
            MediaType = dto.MediaType,
        };

        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Add face associations
        if (dto.FaceIds != null && dto.FaceIds.Count > 0)
        {
            var validFaceIds = await _context.Faces
                .Where(f => dto.FaceIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var faceId in validFaceIds)
            {
                _context.AlbumFaces.Add(new AlbumFace
                {
                    AlbumId = album.Id,
                    FaceId = faceId,
                });
            }
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("User {UserId} created album {AlbumId}", UserId, album.Id);

        return CreatedAtAction(nameof(GetAlbum), new { id = album.Id }, new
        {
            album.Id,
            album.Title,
            album.Description,
            albumType = (int)album.AlbumType,
            mediaType = (int)album.MediaType,
            album.CreatorId,
            album.CreatedAt,
        });
    }

    /// <summary>PUT /api/albums/{id} - Update album (creator only)</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] UpdateAlbumDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var album = await _context.Albums
            .Include(a => a.AlbumFaces)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (album == null)
            return NotFound(new { error = "Album not found" });

        if (album.CreatorId != UserId)
            return Forbid();

        if (dto.Title != null)
            album.Title = dto.Title.Trim();
        if (dto.Description != null)
            album.Description = dto.Description.Trim();
        if (dto.AlbumType.HasValue)
            album.AlbumType = dto.AlbumType.Value;
        if (dto.MediaType.HasValue)
            album.MediaType = dto.MediaType.Value;

        album.UpdatedAt = DateTime.UtcNow;

        // Update face associations if provided
        if (dto.FaceIds != null)
        {
            _context.AlbumFaces.RemoveRange(album.AlbumFaces);

            var validFaceIds = await _context.Faces
                .Where(f => dto.FaceIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var faceId in validFaceIds)
            {
                _context.AlbumFaces.Add(new AlbumFace
                {
                    AlbumId = album.Id,
                    FaceId = faceId,
                });
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated album {AlbumId}", UserId, album.Id);
        return Ok(new
        {
            album.Id,
            album.Title,
            album.Description,
            albumType = (int)album.AlbumType,
            mediaType = (int)album.MediaType,
            album.CreatorId,
            album.CreatedAt,
            album.UpdatedAt,
        });
    }

    /// <summary>DELETE /api/albums/{id} - Delete album (creator only)</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var album = await _context.Albums.FindAsync(id);

        if (album == null)
            return NotFound(new { error = "Album not found" });

        if (album.CreatorId != UserId)
            return Forbid();

        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted album {AlbumId}", UserId, album.Id);
        return NoContent();
    }
}

public class CreateAlbumDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AlbumTypeEnum AlbumType { get; set; } = AlbumTypeEnum.Public;
    public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Image;
    public List<int>? FaceIds { get; set; }
}

public class UpdateAlbumDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public AlbumTypeEnum? AlbumType { get; set; }
    public MediaTypeEnum? MediaType { get; set; }
    public List<int>? FaceIds { get; set; }
}
