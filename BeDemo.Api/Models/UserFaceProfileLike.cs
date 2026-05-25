namespace BeDemo.Api.Models;

/// <summary>
/// A user liked another user's face profile (one row per liker per profile).
/// </summary>
public class UserFaceProfileLike
{
	public int Id { get; set; }

	public int UserFaceProfileId { get; set; }
	public UserFaceProfile UserFaceProfile { get; set; } = null!;

	public string UserId { get; set; } = string.Empty;
	public ApplicationUser User { get; set; } = null!;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
