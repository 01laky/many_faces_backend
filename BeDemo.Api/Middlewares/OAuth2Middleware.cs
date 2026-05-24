/*
 * OAuth2Middleware.cs - Middleware for OAuth2 token request validation
 * 
 * This middleware executes before processing OAuth2 token endpoint.
 * Validates:
 * - Client credentials (client_id and client_secret)
 * - Request signatures (if provided)
 * 
 * Middleware executes only for POST requests to /api/oauth2/token endpoint.
 * If validation fails, returns 401 Unauthorized or 400 Bad Request.
 */

using System.Text;
using System.Text.Json;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Middlewares;

/// <summary>
/// Middleware for OAuth2 token endpoint validation
/// </summary>
public class OAuth2Middleware
{
    private readonly RequestDelegate _next;              // Next middleware in pipeline
    private readonly ILogger<OAuth2Middleware> _logger;  // Logger for logging

    public OAuth2Middleware(
        RequestDelegate next,
        ILogger<OAuth2Middleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Main middleware method - executes for each HTTP request
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Checks if request is to OAuth2 token endpoint and is POST method
        // Middleware executes only for this specific endpoint
        if (context.Request.Path.StartsWithSegments("/api/oauth2/token") &&
            context.Request.Method == "POST")
        {
            // Gets OAuth2Service from dependency injection container
            // Uses RequestServices because OAuth2Service is scoped (new instance for each request)
            var oauth2Service = context.RequestServices.GetRequiredService<IOAuth2Service>();

            // Enables buffering request body - allows reading body multiple times
            // This is necessary because body must be read here and then again in controller
            context.Request.EnableBuffering();

            // Reads request body into string
            var bodyStream = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await bodyStream.ReadToEndAsync();

            // Resets stream position to beginning so controller can read body again
            context.Request.Body.Position = 0;

            try
            {
                // Deserializes JSON body into OAuth2TokenRequest object
                // PropertyNameCaseInsensitive = true means case doesn't matter (e.g., "ClientId" and "clientId" both work)
                var request = JsonSerializer.Deserialize<OAuth2TokenRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // If request was successfully deserialized
                if (request != null)
                {
                    // ============================================================
                    // CLIENT CREDENTIALS VALIDATION
                    // ============================================================
                    // Validates that client_id and client_secret are correct
                    // This ensures only authorized clients can request tokens
                    var isValidClient = await oauth2Service.ValidateClientAsync(
                        request.ClientId,
                        request.ClientSecret);

                    // If client credentials are not valid, returns 401 Unauthorized
                    if (!isValidClient)
                    {
                        _logger.LogWarning(
                            "authFailureReason=invalid_client clientIdHint={ClientIdHint}",
                            string.IsNullOrWhiteSpace(request.ClientId) ? "missing" : request.ClientId);
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new OAuth2ErrorResponse
                        {
                            Error = "invalid_client",                           // OAuth2 error code
                            ErrorDescription = "Client authentication failed"  // Error description
                        }));
                        return;  // Terminates pipeline - request won't reach controller
                    }

                    // ============================================================
                    // Request body ECDSA signatures (O4) — not supported
                    // ============================================================
                    // Previous builds verified against the *server* signing key, which is not a client-PKI model.
                    // Reject any non-empty signature so clients do not rely on a misleading feature; use TLS + client_secret (+ rate limits).
                    if (!string.IsNullOrWhiteSpace(request.Signature) ||
                        !string.IsNullOrWhiteSpace(request.SignatureAlgorithm))
                    {
                        _logger.LogWarning("OAuth2 token request included body signature fields; feature removed (use TLS + client credentials)");
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new OAuth2ErrorResponse
                        {
                            Error = "invalid_request",
                            ErrorDescription = "Request body JWT/ECDSA signatures are not supported; use HTTPS and client_id/client_secret.",
                        }));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // If error occurs while parsing request (e.g., invalid JSON), returns 400 Bad Request
                _logger.LogError(ex, "Error processing OAuth2 request");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new OAuth2ErrorResponse
                {
                    Error = "invalid_request",                    // OAuth2 error code
                    ErrorDescription = "Request parsing failed"  // Error description
                }));
                return;  // Terminates pipeline
            }
        }

        // If request is not to OAuth2 token endpoint, or validation passed,
        // continues to next middleware in pipeline
        await _next(context);
    }
}
