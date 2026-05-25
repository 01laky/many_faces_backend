namespace BeDemo.Api.Models;

/// <summary>
/// Persisted OAuth2 refresh token (A17). Only a one-way hash of the secret is stored; the plaintext is sent once to the client.
/// Rotation: redeem marks <see cref="RevokedAtUtc"/> and inserts a new row; reuse of an old plaintext fails lookup.
/// </summary>
public class OAuthRefreshToken
{
	public int Id { get; set; }

	/// <summary>SHA-256 (hex, lowercase) of the opaque refresh string.</summary>
	public string TokenHash { get; set; } = string.Empty;

	public string UserId { get; set; } = string.Empty;

	public DateTime ExpiresAtUtc { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// When issuing access tokens after refresh, use the same short vs long JWT TTL as the original password grant
	/// (<c>RememberMe == true</c> on first login).
	/// </summary>
	public bool UseRememberMeAccessLifetime { get; set; }

	/// <summary>Set when the token was rotated or invalidated; prevents reuse (one-time refresh).</summary>
	public DateTime? RevokedAtUtc { get; set; }

	/// <summary>Optional: hash of the replacement token for audit chains (A22).</summary>
	public string? ReplacedByTokenHash { get; set; }
}
