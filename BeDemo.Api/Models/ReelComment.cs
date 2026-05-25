namespace BeDemo.Api.Models;

public class ReelComment
{
	public int Id { get; set; }
	public int ReelId { get; set; }
	public string UserId { get; set; } = null!;
	public string Content { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	public Reel Reel { get; set; } = null!;
	public ApplicationUser User { get; set; } = null!;
}
