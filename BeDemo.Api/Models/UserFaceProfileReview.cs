namespace BeDemo.Api.Models;

/// <summary>
/// Review (recenzia) of a user's face profile. One per author per profile. Stars 1–6.
/// </summary>
public class UserFaceProfileReview
{
	public int Id { get; set; }

	public int UserFaceProfileId { get; set; }
	public UserFaceProfile UserFaceProfile { get; set; } = null!;

	public string AuthorUserId { get; set; } = string.Empty;
	public ApplicationUser Author { get; set; } = null!;

	public string Title { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	/// <summary>1–6 stars</summary>
	public byte Stars { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
