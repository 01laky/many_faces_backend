namespace BeDemo.Api.Models;

public class Blog
{
    public int Id { get; set; }
    public string CreatorId { get; set; } = null!;
    public int FaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser Creator { get; set; } = null!;
    public Face Face { get; set; } = null!;
    public ICollection<BlogImage> Images { get; set; } = new List<BlogImage>();
    public ICollection<BlogComment> Comments { get; set; } = new List<BlogComment>();
    public ICollection<BlogLike> Likes { get; set; } = new List<BlogLike>();
}
