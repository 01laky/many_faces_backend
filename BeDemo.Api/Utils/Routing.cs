/*
 * Routing.cs - Helper utilities for routing logic
 * 
 * This file contains helper methods for face path routing:
 * - HasFacePath: Determines if a path should use face routing
 * - ConvertToKebabCase: Converts string to kebab-case format
 */

namespace BeDemo.Api.Utils;

/// <summary>
/// Helper class for routing utilities
/// </summary>
public static class Routing
{
    // Paths that should NOT use face routing (public endpoints)
    // These paths are accessible without face prefix
    // Note: Paths starting with these prefixes are public (e.g., /api/faces, /api/users)
    private static readonly string[] PublicPaths = new[]
    {
        "/api/",        // All /api/* paths are public (e.g., /api/faces, /api/users, /api/pages)
        "/swagger",
        "/swagger-ui",
        "/openapi",
        "/hubs",
    };

    /// <summary>
    /// Determines if a path should use face routing
    /// Returns true if path should be checked for face prefix, false if it's a public path
    /// </summary>
    /// <param name="path">Request path (e.g., "/acme-corp/api/users")</param>
    /// <returns>True if path should use face routing, false if it's public</returns>
    public static bool HasFacePath(string path)
    {
        // Empty or null path is not a face path
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Check if path starts with any public path
        // Public paths are accessible without face prefix
        foreach (var publicPath in PublicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                return false; // This is a public path, don't use face routing
            }
        }

        // All other paths should use face routing
        return true;
    }

    /// <summary>
    /// Converts a string to kebab-case format
    /// Examples: "AcmeCorp" -> "acme-corp", "MyCompany" -> "my-company"
    /// Used to convert Face.Index to URL prefix format
    /// </summary>
    /// <param name="input">Input string to convert</param>
    /// <returns>Kebab-case string</returns>
    public static string ConvertToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Split by uppercase letters (except first)
        // This handles PascalCase and camelCase
        var result = new System.Text.StringBuilder();
        
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            
            // If current character is uppercase and not first character, add hyphen before it
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('-');
            }
            
            // Convert to lowercase and add to result
            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }
}
