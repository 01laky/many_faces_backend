namespace BeDemo.Api.Models;

/// <summary>
/// Flat comment thread on a user's face profile.
/// </summary>
public class UserFaceProfileComment
{
    public int Id { get; set; }

    public int UserFaceProfileId { get; set; }
    public UserFaceProfile UserFaceProfile { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
