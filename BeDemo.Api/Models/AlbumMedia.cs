namespace BeDemo.Api.Models;

/// <summary>Image or video row belonging to an album (portal upload / operator inventory).</summary>
public class AlbumMedia
{
	public int Id { get; set; }
	public int AlbumId { get; set; }
	public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Image;
	public string ImageUrl { get; set; } = string.Empty;
	public string? VideoUrl { get; set; }
	public string? ThumbnailUrl { get; set; }
	public string? Title { get; set; }
	public int SortOrder { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Album Album { get; set; } = null!;
}
