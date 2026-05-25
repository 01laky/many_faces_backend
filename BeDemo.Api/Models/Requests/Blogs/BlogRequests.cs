namespace BeDemo.Api.Models.Requests.Blogs;

public class CreateBlogDto
{
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public int FaceId { get; set; }
	public List<string>? ImageUrls { get; set; }
}

public class UpdateBlogDto
{
	public string? Title { get; set; }
	public string? Content { get; set; }
	public int? FaceId { get; set; }
	public List<string>? ImageUrls { get; set; }
}

public class CreateBlogCommentDto
{
	public string Content { get; set; } = string.Empty;
}

public class UpdateBlogCommentDto
{
	public string Content { get; set; } = string.Empty;
}

