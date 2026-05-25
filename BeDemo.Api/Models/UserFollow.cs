namespace BeDemo.Api.Models;

public class UserFollow
{
	public int Id { get; set; }
	public string FollowerId { get; set; } = null!;
	public string FollowedId { get; set; } = null!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public ApplicationUser Follower { get; set; } = null!;
	public ApplicationUser Followed { get; set; } = null!;
}
