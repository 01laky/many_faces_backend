namespace BeDemo.Api.Models;

/// <summary>
/// Pending email-code signup. <see cref="LinkHash"/> is the opaque <c>?hash=</c> query value; <see cref="CodeHash"/> pairs with the human verification code.
/// </summary>
public sealed class RegistrationInvite
{
	public Guid Id { get; set; }

	public string Email { get; set; } = string.Empty;

	public string NormalizedEmail { get; set; } = string.Empty;

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	/// <summary>Unique lookup key for mail link query parameter <c>hash</c>.</summary>
	public string LinkHash { get; set; } = string.Empty;

	public string CodeHash { get; set; } = string.Empty;

	public int FailedAttemptCount { get; set; }

	public DateTime ExpiresAtUtc { get; set; }

	public DateTime? ConsumedAtUtc { get; set; }

	public DateTime? RevokedAtUtc { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	public string? CreatedByUserId { get; set; }

	public string Locale { get; set; } = "en";

	public int? FaceId { get; set; }
}
