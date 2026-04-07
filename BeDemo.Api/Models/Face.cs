using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models;

/// <summary>
/// Face entity model
/// </summary>
public class Face
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Index { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    /// <summary>
    /// JSON string with gradient settings (type, colors, angle, animation, animationSpeed)
    /// </summary>
    public string? GradientSettings { get; set; }

    /// <summary>
    /// Indicates if this face is public (accessible without authentication) or private (requires authentication)
    /// Default is true (public) for backward compatibility
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// Profile / directory visibility (independent from IsPublic shell routing).
    /// </summary>
    public FaceVisibility Visibility { get; set; } = FaceVisibility.Public;

    /// <summary>
    /// When false, reviews are not shown and cannot be created for profiles in this face.
    /// </summary>
    public bool AllowRecensions { get; set; }

    /// <summary>
    /// When true, non-host users may create user-owned chat rooms from the frontend (+ panel).
    /// </summary>
    public bool ChatRoomsCreate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property - one Face has many Pages
    public ICollection<Page> Pages { get; set; } = new List<Page>();

    // Navigation property - one Face has many UserFaceProfiles
    public ICollection<UserFaceProfile> UserFaceProfiles { get; set; } = new List<UserFaceProfile>();

    public ICollection<FaceChatRoom> ChatRooms { get; set; } = new List<FaceChatRoom>();
}
