namespace BeDemo.Api.Models.Requests.Reels;

public class CreateReelDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string VideoUrl { get; set; } = string.Empty;
	public List<int>? FaceIds { get; set; }
}

public class UpdateReelDto
{
	public string? Title { get; set; }
	public string? Description { get; set; }
	public string? VideoUrl { get; set; }
	public List<int>? FaceIds { get; set; }
}

public class CreateReelCommentDto
{
	public string Content { get; set; } = string.Empty;
}

public class UpdateReelCommentDto
{
	public string Content { get; set; } = string.Empty;
}

