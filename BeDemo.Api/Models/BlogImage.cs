namespace BeDemo.Api.Models;

public class BlogImage
{
	public int Id { get; set; }
	public int BlogId { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public int SortOrder { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Blog Blog { get; set; } = null!;
}
