namespace BeDemo.Api.Models;

public class AlbumFace
{
    public int Id { get; set; }
    public int AlbumId { get; set; }
    public int FaceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Album Album { get; set; } = null!;
    public Face Face { get; set; } = null!;
}
