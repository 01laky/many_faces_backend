/*
 * OAuth2Service.cs - Service for OAuth2 authentication and JWT token generation
 * 
 * This service implements OAuth2 Authorization Code flow with support for:
 * - Password grant type (Resource Owner Password Credentials)
 * - Refresh token grant type
 * - ECDSA signed JWT tokens (ES512 algorithm)
 * - Client credentials validation
 * - Request signature validation using ECDSA
 * 
 * JWT tokens contain claims (statements) about the user:
 * - NameIdentifier (User ID)
 * - Name (Username)
 * - Email
 * - GivenName (First Name)
 * - Surname (Last Name)
 * - Jti (JWT ID - unique token identifier)
 * - Iat (Issued At - token creation time)
 */

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for OAuth2 service - defines contract for OAuth2 operations
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Generates JWT access token and refresh token for user
    /// </summary>
    Task<OAuth2TokenResponse?> GenerateTokenAsync(OAuth2TokenRequest request, UserManager<ApplicationUser> userManager);

    /// <summary>
    /// Validates ECDSA request signature
    /// </summary>
    bool ValidateRequestSignature(OAuth2TokenRequest request);

    /// <summary>
    /// Validates client credentials (client_id and client_secret)
    /// </summary>
    Task<bool> ValidateClientAsync(string? clientId, string? clientSecret);
}

/// <summary>
/// OAuth2 service implementation with ECDSA signing
/// </summary>
public class OAuth2Service : IOAuth2Service
{
    private readonly IECDSAKeyService _keyService;      // Service for managing ECDSA keys
    private readonly IConfiguration _configuration;      // Application configuration (appsettings.json)
    private readonly ILogger<OAuth2Service> _logger;   // Logger for logging events

    public OAuth2Service(
        IECDSAKeyService keyService,
        IConfiguration configuration,
        ILogger<OAuth2Service> logger)
    {
        _keyService = keyService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates JWT access token and refresh token based on OAuth2 request
    /// Supports grant types: "password" and "refresh_token"
    /// </summary>
    public async Task<OAuth2TokenResponse?> GenerateTokenAsync(
        OAuth2TokenRequest request,
        UserManager<ApplicationUser> userManager)
    {
        ApplicationUser? user = null;

        // Decides which grant type is used and authenticates user accordingly
        switch (request.GrantType.ToLower())
        {
            case "password":
                // Password grant type - user provides username and password
                // This is Resource Owner Password Credentials flow

                // Validates that username and password are provided
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("Password grant type missing username or password");
                    return null;
                }

                // Try to find user by email first (most common case)
                user = await userManager.FindByEmailAsync(request.Username);

                // If not found by email, try to find by username (for admin account, etc.)
                if (user == null)
                {
                    user = await userManager.FindByNameAsync(request.Username);
                }

                // Verifies password - if user doesn't exist or password is incorrect, returns null
                if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
                {
                    _logger.LogWarning("Invalid username/email or password for user: {Username}", request.Username);
                    return null;
                }
                break;

            case "refresh_token":
                // Refresh token grant type - user provides refresh token to get new access token
                // Refresh token is a long-term token used to refresh access token without needing to enter credentials again

                // Validates that refresh token is provided
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    _logger.LogWarning("Refresh token grant type missing refresh token");
                    return null;
                }

                // Validates and decodes refresh token
                // In real implementation, refresh token would be stored in database and validated there
                // For this demo implementation, we use Base64 encoded random string as refresh token
                // Access tokens are JWT tokens, so if someone tries to use access token as refresh token, it will fail
                var handler = new JwtSecurityTokenHandler();

                // First check if refresh token is a valid JWT (access tokens are JWT)
                // If it's a valid JWT, it's likely an access token, not a refresh token
                if (handler.CanReadToken(request.RefreshToken))
                {
                    // Try to validate as JWT - if it succeeds, it's an access token, not refresh token
                    var tokenValidationParameters = GetTokenValidationParameters();
                    try
                    {
                        var principal = handler.ValidateToken(request.RefreshToken, tokenValidationParameters, out var validatedToken);
                        // If validation succeeds, this is an access token, not a refresh token
                        _logger.LogWarning("Refresh token is actually an access token");
                        return null;
                    }
                    catch
                    {
                        // If JWT validation fails, it might be a valid refresh token (Base64 string)
                        // But since refresh tokens are not JWT, we should reject it
                        _logger.LogWarning("Refresh token appears to be JWT but validation failed");
                        return null;
                    }
                }

                // Refresh tokens in this implementation are Base64 encoded random strings
                // They are not JWT tokens, so we need to validate them differently
                // For this demo, we cannot validate refresh tokens without storing them in database
                // So we reject all refresh token requests for now
                // In production, refresh tokens should be stored in database and validated there
                _logger.LogWarning("Refresh token validation not implemented - tokens must be stored in database");
                return null;

            default:
                // Unknown or unsupported grant type
                _logger.LogWarning("Unsupported grant type: {GrantType}", request.GrantType);
                return null;
        }

        // If user was not found or authentication failed, returns null
        if (user == null)
        {
            _logger.LogWarning("User not found or authentication failed");
            return null;
        }

        // ============================================================================
        // JWT TOKEN CREATION
        // ============================================================================

        // Creates list of claims (statements) about the user
        // Claims are information that are part of JWT token and can be used for authorization
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),                    // Unique user identifier
            new(ClaimTypes.Name, user.UserName ?? string.Empty),        // Username (email)
            new(ClaimTypes.Email, user.Email ?? string.Empty),          // Email address
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),  // JWT ID - unique identifier for this token
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)  // Token creation time (Unix timestamp)
        };

        // Adds optional claims if they exist
        if (!string.IsNullOrEmpty(user.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }

        if (!string.IsNullOrEmpty(user.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

        // Gets ECDSA signing key and configuration
        var signingKey = _keyService.GetSigningKey();
        var expiresIn = _configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60);  // Default: 60 minutes

        // Creates SecurityTokenDescriptor - describes how token should be created
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),                       // Claims that will be in token
            Expires = DateTime.UtcNow.AddMinutes(expiresIn),            // Token expiration time
            Issuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",    // Who issued the token
            Audience = _configuration["Jwt:Audience"] ?? "BeDemoApi", // Who the token is intended for
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha512),  // ECDSA P-521 with SHA-512 hash
            Claims = new Dictionary<string, object>
            {
                { "key_id", _keyService.GetKeyId() }  // ID of key used for signing (for key rotation)
            }
        };

        // Creates and signs JWT token
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generates refresh token - random Base64 string
        // In production, refresh token should be stored in database with expiration
        var refreshToken = GenerateRefreshToken();

        // Returns OAuth2 token response
        return new OAuth2TokenResponse
        {
            AccessToken = accessToken,           // JWT access token
            TokenType = "Bearer",                // Token type (Bearer is standard for OAuth2)
            ExpiresIn = expiresIn * 60,         // Expiration time in seconds
            RefreshToken = refreshToken,         // Refresh token to refresh access token
            Scope = request.Scope               // Requested scope (optional)
        };
    }

    /// <summary>
    /// Validates ECDSA request signature
    /// Request can be signed using ECDSA algorithm to ensure integrity
    /// </summary>
    public bool ValidateRequestSignature(OAuth2TokenRequest request)
    {
        // If request doesn't contain signature or algorithm, validation fails
        if (string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.SignatureAlgorithm))
        {
            _logger.LogWarning("Request missing signature or algorithm");
            return false;
        }

        // We only support ES512 algorithm (ECDSA with P-521 and SHA-512)
        if (request.SignatureAlgorithm != "ES512")
        {
            _logger.LogWarning("Unsupported signature algorithm: {Algorithm}", request.SignatureAlgorithm);
            return false;
        }

        try
        {
            // Creates canonical message from request parameters
            // This message must be the same as the one that was signed by client
            var message = CreateSignatureMessage(request);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            // Decodes Base64 signature
            var signatureBytes = Convert.FromBase64String(request.Signature);

            // Gets validation key (in this case it's the same key as signing key)
            var validationKey = _keyService.GetValidationKey();
            var ecdsa = validationKey.ECDsa ?? throw new InvalidOperationException("ECDSA key not available");

            // Validates signature using ECDSA VerifyData
            // Returns true if signature is valid (message was signed with correct private key)
            return ecdsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA512);
        }
        catch (Exception ex)
        {
            // If validation fails (e.g., invalid Base64, bad message, etc.), logs error and returns false
            _logger.LogError(ex, "Error validating request signature");
            return false;
        }
    }

    /// <summary>
    /// Validates client credentials (client_id and client_secret)
    /// In production, these should be stored in database or other secure storage
    /// </summary>
    public Task<bool> ValidateClientAsync(string? clientId, string? clientSecret)
    {
        // Loads valid client credentials from configuration
        // In production, they should be stored in database or use OAuth2 Client Store
        var validClientId = _configuration["OAuth2:ClientId"] ?? "be-demo-client";
        var validClientSecret = _configuration["OAuth2:ClientSecret"] ?? "be-demo-secret-very-strong-key";

        // Validates that client_id and client_secret are provided and match valid credentials
        var isValid = !string.IsNullOrEmpty(clientId) &&
                     !string.IsNullOrEmpty(clientSecret) &&
                     clientId == validClientId &&
                     clientSecret == validClientSecret;

        return Task.FromResult(isValid);
    }

    /// <summary>
    /// Creates canonical message from request parameters for signing
    /// Message must always be in the same format for validation to work correctly
    /// </summary>
    private string CreateSignatureMessage(OAuth2TokenRequest request)
    {
        // Creates list of parameters in canonical format
        // Format: key=value&key=value&...
        var parts = new List<string>
        {
            $"grant_type={request.GrantType}",
            $"client_id={request.ClientId ?? ""}",
            $"username={request.Username ?? ""}",
            $"scope={request.Scope ?? ""}",
            $"timestamp={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"  // Timestamp ensures each message is unique
        };

        // Joins all parameters using &
        return string.Join("&", parts);
    }

    /// <summary>
    /// Generates random refresh token
    /// Refresh token is a long-term token used to refresh access token
    /// </summary>
    private string GenerateRefreshToken()
    {
        // Generates 64 random bytes (512 bits)
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        // Converts to Base64 string for easy transmission
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Creates TokenValidationParameters for JWT token validation
    /// These parameters are used when validating refresh tokens
    /// </summary>
    private TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,                    // Validates signing key
            IssuerSigningKey = _keyService.GetValidationKey(),   // Key for validation
            ValidateIssuer = true,                              // Validates issuer
            ValidIssuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
            ValidateAudience = true,                            // Validates audience
            ValidAudience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
            ValidateLifetime = true,                            // Validates expiration
            ClockSkew = TimeSpan.Zero                           // No tolerance for time skew
        };
    }
}
