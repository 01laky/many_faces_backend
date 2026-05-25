namespace BeDemo.Api.Models;

public class StoryFace
{
	public int Id { get; set; }
	public int StoryId { get; set; }
	public int FaceId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Story Story { get; set; } = null!;
	public Face Face { get; set; } = null!;
}
