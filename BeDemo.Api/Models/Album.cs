namespace BeDemo.Api.Models;

public enum AlbumTypeEnum
{
    Public = 1,
    Private = 2,
    Paid = 3
}

public enum MediaTypeEnum
{
    Image = 1,
    Video = 2
}

public class Album
{
    public int Id { get; set; }
    public string CreatorId { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AlbumTypeEnum AlbumType { get; set; } = AlbumTypeEnum.Public;
    public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Image;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser Creator { get; set; } = null!;
    public ICollection<AlbumFace> AlbumFaces { get; set; } = new List<AlbumFace>();
    public ICollection<AlbumComment> Comments { get; set; } = new List<AlbumComment>();
    public ICollection<AlbumLike> Likes { get; set; } = new List<AlbumLike>();
}
