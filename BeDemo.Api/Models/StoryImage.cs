namespace BeDemo.Api.Models;

public class StoryImage
{
	public int Id { get; set; }
	public int StoryId { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int SortOrder { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Story Story { get; set; } = null!;
}
