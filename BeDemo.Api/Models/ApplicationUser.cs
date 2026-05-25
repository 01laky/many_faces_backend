using Microsoft.AspNetCore.Identity;

namespace BeDemo.Api.Models;

/// <summary>
/// Canonical **global** platform identity for OAuth2 JWT authZ (ACL A1): <see cref="UserRoleId"/> / <c>UserRoles.Name</c>
/// is emitted as <c>ClaimTypes.Role</c> at token issue time. ASP.NET Identity’s <c>AspNetRoles</c> / <c>AspNetUserRoles</c> tables
/// exist for historical/Identity reasons but are **not** used to populate bearer role claims — do not mix models without an explicit mapping story.
/// </summary>
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

	/// <summary>
	/// Incremented when sessions must be invalidated (password change, privilege change). Issued JWTs carry claim <c>atv</c> matching this value (J6).
	/// </summary>
	public int AccessTokenVersion { get; set; }
}
