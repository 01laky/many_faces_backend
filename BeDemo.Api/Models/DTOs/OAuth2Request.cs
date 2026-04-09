/*
 * OAuth2Request.cs - Data Transfer Objects (DTOs) for OAuth2 requests and responses
 * 
 * This file contains models used for communication with OAuth2 API.
 * DTOs are simple objects used to transfer data between client and server.
 */

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Request model for OAuth2 token endpoint
/// 
/// Used in POST /api/oauth2/token request
/// </summary>
public class OAuth2TokenRequest
{
    /// <summary>
    /// Grant type - type of OAuth2 flow
    /// Required field
    /// Possible values: "password", "refresh_token"
    /// Accepts camelCase (grantType) via PropertyNameCaseInsensitive
    /// </summary>
    [Required]
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// Client ID - OAuth2 client identifier
    /// Accepts camelCase (clientId) via PropertyNameCaseInsensitive
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client Secret - OAuth2 client secret key
    /// Accepts camelCase (clientSecret) via PropertyNameCaseInsensitive
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Username - used for password grant type
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password - used for password grant type
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Refresh Token - used for refresh_token grant type
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Scope - requested permissions (optional)
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Password grant only: when <c>true</c>, access token lifetime uses <c>Jwt:ExpiresInMinutesRememberMe</c>;
    /// when <c>false</c>, <c>null</c>, or omitted, uses <c>Jwt:ExpiresInMinutes</c> (short session).
    /// Ignored for other grant types once a user is authenticated (same JWT path).
    /// </summary>
    public bool? RememberMe { get; set; }

    /// <summary>
    /// ECDSA Signature - Base64 encoded request signature (optional)
    /// Used to ensure request integrity
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Signature Algorithm - algorithm used for signing
    /// Optional: "ES512" (ECDSA with P-521 and SHA-512) if signature is provided
    /// </summary>
    public string? SignatureAlgorithm { get; set; }
}

/// <summary>
/// Response model for OAuth2 token endpoint
/// 
/// Returned on successful POST /api/oauth2/token request
/// </summary>
public class OAuth2TokenResponse
{
    /// <summary>
    /// Access Token - JWT token used for authenticating API requests
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token Type - token type (always "Bearer" for OAuth2)
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Expires In - token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Refresh Token - token used to refresh access token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Scope - permissions granted to token
    /// </summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Error response model for OAuth2 endpoints
/// 
/// Returned on error in OAuth2 flow
/// </summary>
public class OAuth2ErrorResponse
{
    /// <summary>
    /// Error - OAuth2 error code (e.g., "invalid_client", "invalid_grant", "invalid_request")
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Error Description - human-readable error description
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Error URI - URI with additional error information (optional)
    /// </summary>
    public string? ErrorUri { get; set; }
}
