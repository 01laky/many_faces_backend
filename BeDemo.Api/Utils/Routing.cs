/*
 * Routing.cs — URL rules for face-prefixed vs system-exempt paths.
 *
 * Exempt paths do not carry a face prefix and do not receive HttpContext.Items face scope.
 * All other API traffic must be shaped as /{face-kebab}/api/... so the first segment
 * resolves to a real Face row.
 */

namespace BeDemo.Api.Utils;

/// <summary>
/// Helpers for face-prefix routing and string normalization.
/// </summary>
public static class Routing
{
    /// <summary>
    /// Paths that must work without a leading face segment (OAuth, legacy cookie auth, docs, static files).
    /// Order does not matter; we use prefix checks with OrdinalIgnoreCase.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>/api/oauth2/*</c> — token and registration must be callable before any face context exists.</description></item>
    /// <item><description><c>/api/auth/*</c> — legacy Identity cookie endpoints (if used).</description></item>
    /// <item><description><c>/api/localization/*</c> — static UI i18n bundles before login.</description></item>
    /// <item><description>Swagger / OpenAPI — developer tooling; not tenant data.</description></item>
    /// <item><description><c>/api/uploads/*</c> — HMAC-signed serve endpoint (SHV2 BE-U3); bare <c>/uploads/*</c> is not served anonymously.</description></item>
    /// <item><description><c>/api/profile/*</c> — account-wide user profile (not tenant-scoped).</description></item>
    /// <item><description><c>/api/my/*</c> — caller-scoped "my content" lists (submissions, etc.).</description></item>
    /// </list>
    /// </remarks>
    private static readonly string[] ExemptPathPrefixes =
    {
        "/api/oauth2",
        "/api/auth",
        "/api/localization",
        "/api/profile",
        "/api/my",
        "/swagger",
        "/openapi",
        "/favicon",
        "/api/uploads",
    };

    /// <summary>
    /// True when the request path is exempt from face-prefix routing and from face-scope enforcement.
    /// </summary>
    public static bool IsExemptFromFaceScope(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (var prefix in ExemptPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the path clearly forgot the face prefix: bare <c>/api/...</c> or <c>/hubs/...</c> (not exempt).
    /// Used to return 400 with a helpful message instead of 403 "unknown face".
    /// </summary>
    public static bool IsReservedPathWithoutFacePrefix(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/api", StringComparison.OrdinalIgnoreCase))
            return !IsExemptFromFaceScope(path);

        if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/hubs", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Converts Face.Index (often PascalCase or lowercase) to the first URL segment we match against.
    /// </summary>
    public static string ConvertToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c) && i > 0)
                result.Append('-');
            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }
}
