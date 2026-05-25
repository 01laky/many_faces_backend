namespace BeDemo.Api.Models;

/// <summary>One row per viewer per story (first view time).</summary>
public class StoryView
{
	public int Id { get; set; }
	public int StoryId { get; set; }
	public string ViewerUserId { get; set; } = null!;
	public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

	public Story Story { get; set; } = null!;
	public ApplicationUser Viewer { get; set; } = null!;
}
