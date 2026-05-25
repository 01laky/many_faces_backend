namespace BeDemo.Api.Models;

public class Story
{
	public int Id { get; set; }
	public string CreatorId { get; set; } = null!;
	public string Title { get; set; } = string.Empty;
	public StoryState State { get; set; } = StoryState.Draft;
	/// <summary>When the story becomes visible (UTC). Set on publish.</summary>
	public DateTime? PublishedAt { get; set; }
	/// <summary>Story disappears from public lists after this (UTC). Typically PublishedAt + 24h.</summary>
	public DateTime? ExpiresAt { get; set; }
	/// <summary>When a scheduled publish should run (UTC). Null if not scheduled for future.</summary>
	public DateTime? ScheduledPublishAt { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	public ApplicationUser Creator { get; set; } = null!;
	public ICollection<StoryFace> StoryFaces { get; set; } = new List<StoryFace>();
	public ICollection<StoryImage> Images { get; set; } = new List<StoryImage>();
	public ICollection<StoryLike> Likes { get; set; } = new List<StoryLike>();
	public ICollection<StoryComment> Comments { get; set; } = new List<StoryComment>();
	public ICollection<StoryView> Views { get; set; } = new List<StoryView>();
}
