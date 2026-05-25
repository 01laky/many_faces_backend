namespace BeDemo.Api.Models;

public class StoryLike
{
	public int Id { get; set; }
	public int StoryId { get; set; }
	public string UserId { get; set; } = null!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Story Story { get; set; } = null!;
	public ApplicationUser User { get; set; } = null!;
}
