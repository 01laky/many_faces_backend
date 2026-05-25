namespace BeDemo.Api.Models;

public class BlogLike
{
	public int Id { get; set; }
	public int BlogId { get; set; }
	public string UserId { get; set; } = null!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Blog Blog { get; set; } = null!;
	public ApplicationUser User { get; set; } = null!;
}
