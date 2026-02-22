/*
 * UserProfile.cs - User Profile entity
 * 
 * Represents additional user profile information in a one-to-one relationship with ApplicationUser.
 * This entity is automatically created when a user registers.
 */

namespace BeDemo.Api.Models;

/// <summary>
/// User Profile entity - stores additional user information
/// One-to-one relationship with ApplicationUser
/// </summary>
public class UserProfile
{
    /// <summary>
    /// Primary key - auto-increment integer ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to ApplicationUser - required one-to-one relationship
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to ApplicationUser
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// User's nickname/display name
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// User's age
    /// </summary>
    public int? Age { get; set; }

    /// <summary>
    /// User's gender (rod) - typically "M" (Male), "F" (Female), or "O" (Other)
    /// </summary>
    public string? Rod { get; set; }

    /// <summary>
    /// Timestamp when the profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the profile was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property - one UserProfile has many UserFaceProfiles
    /// </summary>
    public ICollection<UserFaceProfile> UserFaceProfiles { get; set; } = new List<UserFaceProfile>();
}
