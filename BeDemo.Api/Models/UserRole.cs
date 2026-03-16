/*
 * UserRole.cs - User Role entity
 *
 * Roles are either Global (one per user, on ApplicationUser) or Face (per user per face, in UserFaceRole).
 * Global: SUPER_ADMIN, ADMIN, USER, HOST
 * Face: FACE_ADMIN, FACE_USER, INZERENT, SUBSCRIBER, FACE_HOST
 */

namespace BeDemo.Api.Models;

/// <summary>
/// Scope of the role: Global (system-wide, one per user) or Face (per face).
/// </summary>
public enum RoleScope
{
    Global = 0,
    Face = 1
}

/// <summary>
/// User Role entity - defines available roles. Global roles are assigned on ApplicationUser,
/// face roles are assigned per face in UserFaceRole.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Primary key - auto-increment integer ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Role name - unique identifier for the role
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the role
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this role is global (one per user) or face-scoped (per user per face).
    /// </summary>
    public RoleScope Scope { get; set; }

    /// <summary>
    /// Timestamp when the role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation - users with this role as their global role (only for Scope = Global)
    /// </summary>
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    /// <summary>
    /// Navigation - user-face role assignments (only for Scope = Face)
    /// </summary>
    public ICollection<UserFaceRole> UserFaceRoles { get; set; } = new List<UserFaceRole>();

    /// <summary>
    /// Global role name constants (assigned on ApplicationUser)
    /// </summary>
    public static class GlobalRoleNames
    {
        public const string SuperAdmin = "SUPER_ADMIN";
        public const string Admin = "ADMIN";
        public const string User = "USER";
        public const string Host = "HOST";
    }

    /// <summary>
    /// Face role name constants (assigned per face in UserFaceRole)
    /// </summary>
    public static class FaceRoleNames
    {
        public const string FaceAdmin = "FACE_ADMIN";
        public const string FaceUser = "FACE_USER";
        public const string Inzerent = "INZERENT";
        public const string Subscriber = "SUBSCRIBER";
        public const string FaceHost = "FACE_HOST";
    }

    /// <summary>
    /// Legacy alias: User (global role used for registration default)
    /// </summary>
    public static class RoleNames
    {
        public const string User = GlobalRoleNames.User;
        public const string SuperAdmin = GlobalRoleNames.SuperAdmin;
        public const string Admin = GlobalRoleNames.Admin;
        public const string FaceAdmin = FaceRoleNames.FaceAdmin;
        public const string FaceUser = FaceRoleNames.FaceUser;
        public const string Inzerent = FaceRoleNames.Inzerent;
        public const string Subscriber = FaceRoleNames.Subscriber;
        public const string Host = GlobalRoleNames.Host;
        public const string FaceHost = FaceRoleNames.FaceHost;
    }
}
