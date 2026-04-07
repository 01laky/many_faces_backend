namespace BeDemo.Api.Models;

public class AlbumLike
{
    public int Id { get; set; }
    public int AlbumId { get; set; }
    public string UserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Album Album { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
