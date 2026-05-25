using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Validates OAuth2 confidential client credentials (<c>client_id</c> / <c>client_secret</c>) against persisted
/// <see cref="OAuthClient"/> rows (O1). Secrets are stored hashed; verification uses <see cref="IPasswordHasher{OAuthClient}"/>.
/// </summary>
public interface IOAuthClientValidator
{
	/// <summary>
	/// Returns <c>true</c> when an <strong>active</strong> client exists for <paramref name="clientId"/> and the secret verifies.
	/// </summary>
	/// <param name="clientId">Registered client identifier.</param>
	/// <param name="clientSecret">Plaintext secret from the token request body.</param>
	Task<bool> ValidateAsync(string? clientId, string? clientSecret);
}

/// <inheritdoc cref="IOAuthClientValidator" />
public sealed class OAuthClientValidator : IOAuthClientValidator
{
	private readonly ApplicationDbContext _db;
	private readonly IPasswordHasher<OAuthClient> _oauthClientHasher;
	private readonly ILogger<OAuthClientValidator> _logger;

	/// <summary>Creates the validator.</summary>
	public OAuthClientValidator(
		ApplicationDbContext db,
		IPasswordHasher<OAuthClient> oauthClientHasher,
		ILogger<OAuthClientValidator> logger)
	{
		_db = db;
		_oauthClientHasher = oauthClientHasher;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> ValidateAsync(string? clientId, string? clientSecret)
	{
		if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
			return false;

		var row = await _db.OAuthClients.AsNoTracking()
			.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive)
			.ConfigureAwait(false);
		if (row == null)
		{
			// Avoid leaking whether the id exists when inactive — same false path as missing.
			_logger.LogDebug("OAuth client not found or inactive: {ClientId}", clientId);
			return false;
		}

		var r = _oauthClientHasher.VerifyHashedPassword(row, row.SecretHash, clientSecret);
		return r is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
	}
}
