/*
 * UserRole.cs - User Role entity
 * 
 * Represents user roles in the system. Each user has one role.
 * Roles: SUPER_ADMIN, ADMIN, FACE_ADMIN, INZERENT, SUBSCRIBER, USER
 */

namespace BeDemo.Api.Models;

/// <summary>
/// User Role entity - defines available roles in the system
/// Each user has one role (many-to-one relationship)
/// </summary>
public class UserRole
{
    /// <summary>
    /// Primary key - auto-increment integer ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Role name - unique identifier for the role
    /// Values: SUPER_ADMIN, ADMIN, FACE_ADMIN, INZERENT, SUBSCRIBER, USER
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the role
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp when the role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property - users with this role
    /// </summary>
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    /// <summary>
    /// Role name constants
    /// </summary>
    public static class RoleNames
    {
        public const string SuperAdmin = "SUPER_ADMIN";
        public const string Admin = "ADMIN";
        public const string FaceAdmin = "FACE_ADMIN";
        public const string Inzerent = "INZERENT";
        public const string Subscriber = "SUBSCRIBER";
        public const string User = "USER";
    }
}
