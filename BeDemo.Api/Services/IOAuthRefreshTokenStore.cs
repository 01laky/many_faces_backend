namespace BeDemo.Api.Services;

/// <summary>
/// Database-backed refresh tokens with rotation (ACL A17). Used by <see cref="OAuth2Service"/> after password grant.
/// </summary>
public interface IOAuthRefreshTokenStore
{
	/// <summary>Persist a new refresh token after successful password grant.</summary>
	Task CreateAsync(string userId, string plainRefreshToken, bool useRememberMeAccessLifetime, CancellationToken cancellationToken = default);

	/// <summary>
	/// Validates plaintext, revokes the row (single-use), persists a new opaque refresh token, returns data for the OAuth2 response.
	/// Returns null if invalid, expired, already used, or wrong shape.
	/// </summary>
	Task<RefreshTokenRedeemResult?> RedeemAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken = default);

	/// <summary>
	/// Revokes all non-revoked refresh tokens for the user (password change / forced logout, J6).
	/// </summary>
	Task RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <param name="UserId">AspNetUsers Id for the token owner.</param>
/// <param name="UseRememberMeAccessLifetime">Maps to Jwt remember vs session expiry for the new access token.</param>
/// <param name="NewPlainRefreshToken">Return this to the client; old plaintext is now invalid.</param>
public sealed record RefreshTokenRedeemResult(string UserId, bool UseRememberMeAccessLifetime, string NewPlainRefreshToken);
