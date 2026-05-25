namespace BeDemo.Api.Models;

public class AlbumComment
{
	public int Id { get; set; }
	public int AlbumId { get; set; }
	public string UserId { get; set; } = null!;
	public string Content { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	public Album Album { get; set; } = null!;
	public ApplicationUser User { get; set; } = null!;
}
