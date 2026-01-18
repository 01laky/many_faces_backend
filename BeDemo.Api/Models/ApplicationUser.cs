using Microsoft.AspNetCore.Identity;

namespace BeDemo.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Foreign key to UserRole - many-to-one relationship
    /// </summary>
    public int UserRoleId { get; set; }

    /// <summary>
    /// Navigation property to UserRole
    /// </summary>
    public UserRole UserRole { get; set; } = null!;

    /// <summary>
    /// Navigation property to UserProfile - one-to-one relationship
    /// </summary>
    public UserProfile? UserProfile { get; set; }
}
