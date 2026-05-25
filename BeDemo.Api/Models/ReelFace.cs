namespace BeDemo.Api.Models;

/// <summary>
/// When empty for a reel (no rows), the reel is visible on every face.
/// When rows exist, the reel is only visible on the linked faces.
/// </summary>
public class ReelFace
{
	public int Id { get; set; }
	public int ReelId { get; set; }
	public int FaceId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Reel Reel { get; set; } = null!;
	public Face Face { get; set; } = null!;
}
