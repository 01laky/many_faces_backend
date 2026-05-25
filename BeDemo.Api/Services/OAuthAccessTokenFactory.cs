using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Issues ES512 access JWTs for the OAuth2 token endpoint and detects when a client mistakenly sends a valid access JWT
/// as <c>refresh_token</c> (must be rejected before hitting the opaque refresh store).
/// </summary>
/// <remarks>
/// Global role name and <see cref="ApplicationUser.AccessTokenVersion"/> are read from the database at issue time so
/// admin promotion and J6 invalidation apply on refresh without requiring a password grant.
/// </remarks>
public interface IOAuthAccessTokenFactory
{
	/// <summary>Creates a signed access JWT and returns its configured lifetime in minutes.</summary>
	/// <param name="user">Identity user; <see cref="ApplicationUser.Id"/> must exist in <c>AspNetUsers</c>.</param>
	/// <param name="useRememberMeAccessLifetime">When <c>true</c>, uses <c>Jwt:ExpiresInMinutesRememberMe</c>; otherwise session TTL.</param>
	Task<(string AccessToken, int ExpiresInMinutes)> CreateAsync(ApplicationUser user, bool useRememberMeAccessLifetime);

	/// <summary>
	/// <c>true</c> when <paramref name="token"/> parses and validates as a currently acceptable access JWT for this API —
	/// in that case the refresh grant must fail (client sent access token instead of opaque refresh).
	/// </summary>
	bool IsValidAccessTokenMisusedAsRefresh(string token);
}

/// <inheritdoc cref="IOAuthAccessTokenFactory" />
/// <remarks>SHV2 BE-A2: uses validated <see cref="JwtTokenLifetimeOptions"/> for access-token TTL selection.</remarks>
public sealed class OAuthAccessTokenFactory : IOAuthAccessTokenFactory
{
	private readonly IECDSAKeyService _keyService;
	private readonly IConfiguration _configuration;
	private readonly JwtTokenLifetimeOptions _jwtLifetimes;
	private readonly ApplicationDbContext _db;
	private readonly ILogger<OAuthAccessTokenFactory> _logger;

	/// <summary>Creates the factory.</summary>
	public OAuthAccessTokenFactory(
		IECDSAKeyService keyService,
		IConfiguration configuration,
		IOptions<JwtTokenLifetimeOptions> jwtLifetimes,
		ApplicationDbContext db,
		ILogger<OAuthAccessTokenFactory> logger)
	{
		_keyService = keyService;
		_configuration = configuration;
		_jwtLifetimes = jwtLifetimes.Value;
		_db = db;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<(string AccessToken, int ExpiresInMinutes)> CreateAsync(
		ApplicationUser user,
		bool useRememberMeAccessLifetime)
	{
		// Standard OIDC-style identity claims; role and atv come from authoritative DB state.
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, user.Id),
			new(ClaimTypes.Name, user.UserName ?? string.Empty),
			new(ClaimTypes.Email, user.Email ?? string.Empty),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
		};

		if (!string.IsNullOrEmpty(user.FirstName))
			claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
		if (!string.IsNullOrEmpty(user.LastName))
			claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

		var row = await _db.Users
			.AsNoTracking()
			.Where(u => u.Id == user.Id)
			.Select(u => new { u.AccessTokenVersion, GlobalRoleName = u.UserRole.Name })
			.FirstAsync()
			.ConfigureAwait(false);
		if (!string.IsNullOrEmpty(row.GlobalRoleName))
			claims.Add(new Claim(ClaimTypes.Role, row.GlobalRoleName));
		claims.Add(new Claim(BeDemoClaimTypes.AccessTokenVersion, row.AccessTokenVersion.ToString(), ClaimValueTypes.Integer32));

		var signingKey = _keyService.GetSigningKey();
		// BE-A2: bound + validated JwtTokenLifetimeOptions — never issue multi-year access JWTs from stale config.
		var expiresInMinutes = _jwtLifetimes.ResolveAccessTokenMinutes(useRememberMeAccessLifetime);

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new ClaimsIdentity(claims),
			Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
			Issuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
			Audience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
			SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha512),
			Claims = new Dictionary<string, object> { { "key_id", _keyService.GetKeyId() } },
		};

		var tokenHandler = new JwtSecurityTokenHandler();
		var token = tokenHandler.CreateToken(tokenDescriptor);
		var accessToken = tokenHandler.WriteToken(token);
		return (accessToken, expiresInMinutes);
	}

	/// <inheritdoc />
	public bool IsValidAccessTokenMisusedAsRefresh(string token)
	{
		var handler = new JwtSecurityTokenHandler();
		if (!handler.CanReadToken(token))
			return false;
		try
		{
			handler.ValidateToken(token, GetTokenValidationParameters(), out _);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogTrace(ex, "String is not a valid access JWT (expected for opaque refresh)");
			return false;
		}
	}

	/// <summary>Parameters aligned with JwtBearer + <see cref="IECDSAKeyService"/> for misuse detection only.</summary>
	private TokenValidationParameters GetTokenValidationParameters()
	{
		return new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKeys = _keyService.GetIssuerSigningKeys().ToList(),
			ValidateIssuer = true,
			ValidIssuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
			ValidateAudience = true,
			ValidAudience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero,
			ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha512 },
		};
	}
}
