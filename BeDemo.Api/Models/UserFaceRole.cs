/*
 * UserFaceRole.cs - User's role within a specific face
 *
 * Each user has one face role per face (e.g. FACE_HOST by default when assigned to a face).
 */

namespace BeDemo.Api.Models;

/// <summary>
/// User's role within a specific face. One row per (UserId, FaceId).
/// UserRoleId must reference a role with Scope = Face.
/// </summary>
public class UserFaceRole
{
    /// <summary>
    /// User ID (ApplicationUser)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Face ID
    /// </summary>
    public int FaceId { get; set; }

    /// <summary>
    /// Face role (must be Scope = Face)
    /// </summary>
    public int UserRoleId { get; set; }

    /// <summary>
    /// When the role was assigned
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Face Face { get; set; } = null!;
    public UserRole UserRole { get; set; } = null!;
}
