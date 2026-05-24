/*
 * OAuth2Service.cs — token endpoint orchestration for password and refresh_token grants.
 *
 * Split responsibilities (see docs):
 *   <see cref="IOAuthClientValidator"/> — client_id / client_secret vs OAuthClients.
 *   <see cref="IOAuthAccessTokenFactory"/> — ES512 access JWT + “access JWT as refresh” guard.
 *   <see cref="IOAuthTokenRequestSignatureVerifier"/> — legacy body signature verification (middleware rejects new usage).
 *   <see cref="IOAuthRefreshTokenStore"/> — opaque refresh persistence and rotation (A17).
 */

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <summary>
/// Facade for the OAuth2 token endpoint: issues tokens and exposes validation helpers used by <c>OAuth2Middleware</c>.
/// </summary>
public interface IOAuth2Service
{
    /// <summary>Runs password or refresh_token grant; returns <c>null</c> on credential / grant errors (controller maps to 401).</summary>
    Task<OAuth2TokenResponse?> GenerateTokenAsync(OAuth2TokenRequest request, UserManager<ApplicationUser> userManager);

    /// <summary>Delegates to <see cref="IOAuthTokenRequestSignatureVerifier"/> (legacy path).</summary>
    bool ValidateRequestSignature(OAuth2TokenRequest request);

    /// <summary>Delegates to <see cref="IOAuthClientValidator"/>.</summary>
    Task<bool> ValidateClientAsync(string? clientId, string? clientSecret);
}

/// <inheritdoc cref="IOAuth2Service" />
public sealed class OAuth2Service : IOAuth2Service
{
    private readonly IOAuthAccessTokenFactory _accessTokens;
    private readonly IOAuthClientValidator _clientValidator;
    private readonly IOAuthTokenRequestSignatureVerifier _signatureVerifier;
    private readonly IOAuthRefreshTokenStore _refreshTokens;
    private readonly ILogger<OAuth2Service> _logger;

    /// <summary>Creates the orchestrator (scoped per HTTP request).</summary>
    public OAuth2Service(
        IOAuthAccessTokenFactory accessTokens,
        IOAuthClientValidator clientValidator,
        IOAuthTokenRequestSignatureVerifier signatureVerifier,
        IOAuthRefreshTokenStore refreshTokens,
        ILogger<OAuth2Service> logger)
    {
        _accessTokens = accessTokens;
        _clientValidator = clientValidator;
        _signatureVerifier = signatureVerifier;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> ValidateClientAsync(string? clientId, string? clientSecret) =>
        _clientValidator.ValidateAsync(clientId, clientSecret);

    /// <inheritdoc />
    public bool ValidateRequestSignature(OAuth2TokenRequest request) =>
        _signatureVerifier.IsSignatureValid(request);

    /// <inheritdoc />
    public async Task<OAuth2TokenResponse?> GenerateTokenAsync(
        OAuth2TokenRequest request,
        UserManager<ApplicationUser> userManager)
    {
        switch (request.GrantType.ToLowerInvariant())
        {
            case "password":
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("Password grant type missing username or password");
                    return null;
                }

                var userByCreds = await userManager.FindByEmailAsync(request.Username)
                    .ConfigureAwait(false)
                    ?? await userManager.FindByNameAsync(request.Username).ConfigureAwait(false);
                if (userByCreds == null || !await userManager.CheckPasswordAsync(userByCreds, request.Password).ConfigureAwait(false))
                {
                    // BE-L1: never log raw username/email on failed password grant (credential stuffing forensics use hash only).
                    _logger.LogWarning(
                        "authFailureReason=invalid_grant {CredentialHint}",
                        PiiLogRedaction.FormatCredentialIdentifierForLog(request.Username));
                    return null;
                }

                if (await userManager.IsLockedOutAsync(userByCreds).ConfigureAwait(false))
                {
                    _logger.LogWarning(
                        "authFailureReason=account_locked_out userId={UserId}",
                        userByCreds.Id);
                    return null;
                }

                var useRememberMe = request.RememberMe == true;
                var (accessPw, minutesPw) = await _accessTokens.CreateAsync(userByCreds, useRememberMe).ConfigureAwait(false);
                var refreshPlain = GenerateOpaqueRefreshToken();
                await _refreshTokens.CreateAsync(userByCreds.Id, refreshPlain, useRememberMe).ConfigureAwait(false);
                return new OAuth2TokenResponse
                {
                    AccessToken = accessPw,
                    TokenType = "Bearer",
                    ExpiresIn = minutesPw * 60,
                    RefreshToken = refreshPlain,
                    Scope = request.Scope,
                };

            case "refresh_token":
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    _logger.LogWarning("Refresh token grant type missing refresh token");
                    return null;
                }

                if (_accessTokens.IsValidAccessTokenMisusedAsRefresh(request.RefreshToken))
                {
                    _logger.LogWarning("Client sent a valid access JWT as refresh_token; rejected");
                    return null;
                }

                var redeem = await _refreshTokens.RedeemAndRotateAsync(request.RefreshToken).ConfigureAwait(false);
                if (redeem == null)
                {
                    _logger.LogWarning("Refresh token redeem failed (unknown, expired, or reused)");
                    return null;
                }

                var userFromRefresh = await userManager.FindByIdAsync(redeem.UserId).ConfigureAwait(false);
                if (userFromRefresh == null)
                {
                    _logger.LogWarning("Refresh token referred to missing user {UserId}", redeem.UserId);
                    return null;
                }

                if (await userManager.IsLockedOutAsync(userFromRefresh).ConfigureAwait(false))
                {
                    _logger.LogWarning("Refresh grant rejected: user account is locked out ({UserId})", userFromRefresh.Id);
                    return null;
                }

                var (accessRf, minutesRf) = await _accessTokens.CreateAsync(userFromRefresh, redeem.UseRememberMeAccessLifetime)
                    .ConfigureAwait(false);
                return new OAuth2TokenResponse
                {
                    AccessToken = accessRf,
                    TokenType = "Bearer",
                    ExpiresIn = minutesRf * 60,
                    RefreshToken = redeem.NewPlainRefreshToken,
                    Scope = request.Scope,
                };

            default:
                _logger.LogWarning("Unsupported grant type: {GrantType}", request.GrantType);
                return null;
        }
    }

    /// <summary>64 bytes from a CSPRNG, Base64-encoded — opaque refresh string returned to the client (stored hashed server-side).</summary>
    private static string GenerateOpaqueRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
