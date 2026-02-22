/*
 * UserFaceProfile.cs - User Face Profile entity
 * 
 * Represents a user's profile within a specific face (tenant context).
 * This entity allows users to have different profiles/contexts per face.
 * Many-to-one relationship with both UserProfile and Face.
 */

namespace BeDemo.Api.Models;

/// <summary>
/// User Face Profile entity - stores user profile information within a specific face context
/// Many-to-one relationship with UserProfile and Face
/// </summary>
public class UserFaceProfile
{
    /// <summary>
    /// Primary key - auto-increment integer ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to UserProfile - required many-to-one relationship
    /// </summary>
    public int UserProfileId { get; set; }

    /// <summary>
    /// Navigation property to UserProfile
    /// </summary>
    public UserProfile UserProfile { get; set; } = null!;

    /// <summary>
    /// Foreign key to Face - required many-to-one relationship
    /// </summary>
    public int FaceId { get; set; }

    /// <summary>
    /// Navigation property to Face
    /// </summary>
    public Face Face { get; set; } = null!;

    /// <summary>
    /// Face-specific display name for the user
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Face-specific avatar URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Face-specific user settings (stored as JSON)
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Whether the user is active in this face
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the profile was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
