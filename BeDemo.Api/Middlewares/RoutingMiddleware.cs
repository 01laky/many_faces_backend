/*
 * RoutingMiddleware.cs - Middleware for multi-tenant routing with face prefixes
 * 
 * This middleware implements face-based routing where each tenant (face) has its own URL prefix.
 * 
 * How it works:
 * 1. URLs like /acme-corp/api/users are rewritten to /api/users?requestFaceID=123
 * 2. Face prefix (first segment) is matched against Face.Index (converted to kebab-case)
 * 3. If face is found, Path is rewritten and requestFaceID query parameter is added
 * 4. If face is not found and path requires face routing, returns 403 Forbidden
 * 5. Public paths (like /api/oauth2, /api/auth, /swagger) are not affected
 * 
 * Caching:
 * - Faces are cached in MemoryCache for 5 minutes to avoid database queries on every request
 * - Cache is automatically refreshed every 5 minutes
 * 
 * Middleware executes before authentication, allowing face-scoped public endpoints.
 */

using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace BeDemo.Api.Middlewares;

/// <summary>
/// Middleware for face-based multi-tenant routing
/// </summary>
public class RoutingMiddleware
{
    private readonly RequestDelegate _next;                      // Next middleware in pipeline
    private readonly IMemoryCache _memoryCache;                  // Memory cache for faces
    private static readonly char[] Separator = { '/' };         // Path separator for splitting

    public RoutingMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache)
    {
        _next = next;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Main middleware method - executes for each HTTP request
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value;

        // Check if this path should use face routing
        // Public paths (like /api/oauth2, /api/auth, /swagger) don't use face routing
        if (path != null && Routing.HasFacePath(path))
        {
            // Split path into segments: /acme-corp/api/users -> ["acme-corp", "api", "users"]
            var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            // Get faces from cache or database
            var faces = GetFaces(serviceProvider);

            // Path must have at least 2 segments for face routing: /face-prefix/rest-of-path
            if (segments.Length > 1)
            {
                // First segment is the face prefix (e.g., "acme-corp")
                var prefix = segments[0];

                // Find matching face by comparing prefix with Face.Index converted to kebab-case
                // Example: "acme-corp" matches Face with Index="AcmeCorp"
                var matchingFace = faces.FirstOrDefault(f =>
                    Routing.ConvertToKebabCase(f.Index) == prefix);

                // If face is found, check if it's public or requires authentication
                if (matchingFace != null)
                {
                    // Check if face is private (requires authentication)
                    if (!matchingFace.IsPublic)
                    {
                        // Private face requires authentication - check if user is authenticated
                        if (context.User?.Identity?.IsAuthenticated != true)
                        {
                            // User is not authenticated and face is private - return 401 Unauthorized
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsync("Authentication required for private face");
                            return;
                        }
                    }

                    // Reconstruct path without face prefix: /api/users
                    // segments[0] is face prefix, rest is the actual path
                    var newPath = "/" + string.Join("/", segments, 1, segments.Length - 1);

                    // Add requestFaceID query parameter to identify which face scope we're in
                    // Controllers can access this via: Request.Query["requestFaceID"]
                    context.Request.QueryString = context.Request.QueryString.Add("requestFaceID", matchingFace.Id.ToString());

                    // Rewrite path without face prefix
                    context.Request.Path = newPath;

                    // Log that we're using face scope (for debugging)
                    Console.WriteLine("Using face scope");
                    Console.WriteLine(matchingFace.Index);

                    // Continue to next middleware (request continues with new path)
                    await _next(context);
                    return;
                }
            }

            // Face path was detected but no matching face found
            // Return 403 Forbidden to prevent unauthorized access
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Face path not allowed");
            return;
        }

        // Path is public or doesn't require face routing
        // Continue normally without modification
        await _next(context);
        Console.WriteLine("Using public scope");
    }

    /// <summary>
    /// Gets faces from cache or database
    /// Faces are cached for 5 minutes to improve performance
    /// </summary>
    /// <param name="serviceProvider">Service provider to get FaceService</param>
    /// <returns>List of faces from cache or database</returns>
    private List<Face> GetFaces(IServiceProvider serviceProvider)
    {
        // Try to get faces from cache first
        // Cache key "Faces" stores the list of all faces
        if (!_memoryCache.TryGetValue("Faces", out List<Face>? faces) || faces == null)
        {
            // Cache miss or expired - get faces from database
            Console.WriteLine("Caching faces");

            // Get FaceService from dependency injection container
            var faceService = serviceProvider.GetRequiredService<IFaceService>();

            // Retrieve faces from database
            faces = faceService.GetFaces();

            // Store faces in cache for 5 minutes
            // After 5 minutes, cache expires and faces will be reloaded from database
            _memoryCache.Set("Faces", faces, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
        }

        // Return faces from cache or database
        // If faces is still null (shouldn't happen, but handle gracefully), return empty list
        return faces ?? new List<Face>();
    }
}
