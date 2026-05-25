using System.Security.Cryptography;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;

namespace BeDemo.Api.Services;

/// <inheritdoc />
public sealed class OAuthRefreshTokenStore : IOAuthRefreshTokenStore
{
	/// <summary>
	/// In-memory EF has no SERIALIZABLE isolation; serialize refresh redemption so integration tests can assert
	/// single-use rotation under concurrent HTTP (matches PostgreSQL behavior for the same plaintext).
	/// </summary>
	private static readonly SemaphoreSlim InMemoryRefreshRedeemGate = new(1, 1);

	private readonly ApplicationDbContext _db;
	private readonly IConfiguration _configuration;
	private readonly ILogger<OAuthRefreshTokenStore> _logger;

	public OAuthRefreshTokenStore(ApplicationDbContext db, IConfiguration configuration, ILogger<OAuthRefreshTokenStore> logger)
	{
		_db = db;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task CreateAsync(string userId, string plainRefreshToken, bool useRememberMeAccessLifetime, CancellationToken cancellationToken = default)
	{
		// Longer refresh window when the client asked for "remember me" on password grant — still bounded (config).
		var sessionDays = _configuration.GetValue("Jwt:RefreshTokenDaysSession", 14);
		var rememberDays = _configuration.GetValue("Jwt:RefreshTokenDaysRememberMe", 90);
		var days = useRememberMeAccessLifetime ? rememberDays : sessionDays;

		var now = DateTime.UtcNow;
		var entity = new OAuthRefreshToken
		{
			TokenHash = TokenHasher.Sha256Hex(plainRefreshToken),
			UserId = userId,
			CreatedAtUtc = now,
			ExpiresAtUtc = now.AddDays(days),
			UseRememberMeAccessLifetime = useRememberMeAccessLifetime,
		};

		_db.OAuthRefreshTokens.Add(entity);
		await _db.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Issued refresh token for user {UserId}, expires {Expires:o}, rememberMeLifetime={Remember}",
			userId, entity.ExpiresAtUtc, useRememberMeAccessLifetime);
	}

	/// <inheritdoc />
	public async Task<RefreshTokenRedeemResult?> RedeemAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken = default)
	{
		var newPlainRefreshToken = CreateOpaqueRefreshToken();
		var hash = TokenHasher.Sha256Hex(plainRefreshToken);
		var newHash = TokenHasher.Sha256Hex(newPlainRefreshToken);
		var now = DateTime.UtcNow;

		var isInMemory = string.Equals(
			_db.Database.ProviderName,
			"Microsoft.EntityFrameworkCore.InMemory",
			StringComparison.Ordinal);

		async Task<RefreshTokenRedeemResult?> RedeemBodyAsync()
		{
			var row = await _db.OAuthRefreshTokens
				.Where(t => t.TokenHash == hash && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
				.FirstOrDefaultAsync(cancellationToken);

			if (row == null)
				return null;

			// Single-use: mark old token consumed so replay of the same plaintext fails the next lookup (A17 rotation).
			row.RevokedAtUtc = now;
			row.ReplacedByTokenHash = newHash;

			var sessionDays = _configuration.GetValue("Jwt:RefreshTokenDaysSession", 14);
			var rememberDays = _configuration.GetValue("Jwt:RefreshTokenDaysRememberMe", 90);
			var days = row.UseRememberMeAccessLifetime ? rememberDays : sessionDays;

			_db.OAuthRefreshTokens.Add(new OAuthRefreshToken
			{
				TokenHash = newHash,
				UserId = row.UserId,
				CreatedAtUtc = now,
				ExpiresAtUtc = now.AddDays(days),
				UseRememberMeAccessLifetime = row.UseRememberMeAccessLifetime,
			});

			await _db.SaveChangesAsync(cancellationToken);

			_logger.LogInformation(
				"Rotated refresh token for user {UserId} (audit: old hash prefix {OldPrefix})",
				row.UserId, hash.Length >= 12 ? hash[..12] : hash);

			return new RefreshTokenRedeemResult(row.UserId, row.UseRememberMeAccessLifetime, newPlainRefreshToken);
		}

		if (isInMemory)
		{
			await InMemoryRefreshRedeemGate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				return await RedeemBodyAsync().ConfigureAwait(false);
			}
			finally
			{
				InMemoryRefreshRedeemGate.Release();
			}
		}

		// PostgreSQL: Serializable reduces double-spend of the same refresh string under concurrency.
		await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		try
		{
			var result = await RedeemBodyAsync();
			if (result == null)
			{
				await tx.RollbackAsync(cancellationToken);
				return null;
			}

			await tx.CommitAsync(cancellationToken);
			return result;
		}
		catch
		{
			await tx.RollbackAsync(cancellationToken);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var rows = await _db.OAuthRefreshTokens
			.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
			.ToListAsync(cancellationToken);
		foreach (var row in rows)
			row.RevokedAtUtc = now;
		if (rows.Count > 0)
		{
			await _db.SaveChangesAsync(cancellationToken);
			_logger.LogInformation("Revoked {Count} refresh token(s) for user {UserId}", rows.Count, userId);
		}
	}

	private static string CreateOpaqueRefreshToken()
	{
		var randomBytes = new byte[64];
		RandomNumberGenerator.Fill(randomBytes);
		return Convert.ToBase64String(randomBytes);
	}
}
