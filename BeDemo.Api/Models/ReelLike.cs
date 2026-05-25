namespace BeDemo.Api.Models;

public class ReelLike
{
	public int Id { get; set; }
	public int ReelId { get; set; }
	public string UserId { get; set; } = null!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Reel Reel { get; set; } = null!;
	public ApplicationUser User { get; set; } = null!;
}
