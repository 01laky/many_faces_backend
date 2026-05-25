using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models.Requests.Admin;

public sealed class UpdateAdminMeProfileRequest
{
	[EmailAddress]
	[MaxLength(256)]
	public string? Email { get; set; }

	[MaxLength(100)]
	public string? FirstName { get; set; }

	[MaxLength(100)]
	public string? LastName { get; set; }

	/// <summary>Rejected — global role cannot be changed via self-profile (SAP-B4).</summary>
	public int? UserRoleId { get; set; }
}

public sealed class UpdateAdminMePasswordRequest
{
	[Required]
	public string CurrentPassword { get; set; } = null!;

	[Required]
	public string NewPassword { get; set; } = null!;

	[Required]
	[Compare(nameof(NewPassword))]
	public string ConfirmPassword { get; set; } = null!;
}

public sealed class ConfirmEmailQuery
{
	[Required]
	public string UserId { get; set; } = null!;

	[Required]
	public string Token { get; set; } = null!;
}
