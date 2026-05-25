namespace BeDemo.Api.Models.Requests.Stories;

public class CreateStoryDto
{
	public string Title { get; set; } = string.Empty;
	public List<int>? FaceIds { get; set; }
}

public class PublishStoryDto
{
	/// <summary>When set and in the future, story stays <see cref="StoryState.Scheduled"/> until then.</summary>
	public DateTime? ScheduledPublishAt { get; set; }
}

public class CreateStoryCommentDto
{
	public string Content { get; set; } = string.Empty;
}

