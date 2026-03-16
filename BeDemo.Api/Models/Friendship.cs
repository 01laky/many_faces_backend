namespace BeDemo.Api.Models;

public class Friendship
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string FriendId { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser Friend { get; set; } = null!;
}
