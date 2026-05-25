using System.Net;
using System.Text.RegularExpressions;
using BeDemo.Api.Models.DTOs.Search;
using ManyFaces.Search.V1;

namespace BeDemo.Api.Services.Search;

/// <summary>
/// XSS-safe projection of autocomplete hits before JSON serialization (§3.5).
/// Highlights allow only <c>&lt;em&gt;</c> / <c>&lt;/em&gt;</c>; title and subtitle are HTML-encoded.
/// </summary>
public static class SearchAutocompleteSanitizer
{
    private static readonly Regex EmTagRegex = new(
        @"</?em>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>HTML-encodes plain text fields.</summary>
    public static string EncodePlainText(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>Strips all tags except lowercase <c>em</c>, then HTML-encodes text segments.</summary>
    public static string SanitizeHighlight(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value.Replace("<EM>", "<em>", StringComparison.OrdinalIgnoreCase)
            .Replace("</EM>", "</em>", StringComparison.OrdinalIgnoreCase);

        // Remove script blocks and event handlers outright before tag parsing.
        normalized = Regex.Replace(normalized, @"<script\b[^>]*>.*?</script>", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, @"\son\w+\s*=\s*(""[^""]*""|'[^']*')", string.Empty,
            RegexOptions.IgnoreCase);

        var parts = Regex.Split(normalized, @"(</?em>)", RegexOptions.IgnoreCase);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var part in parts)
        {
            if (string.Equals(part, "<em>", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "</em>", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(part.Equals("<em>", StringComparison.OrdinalIgnoreCase) ? "<em>" : "</em>");
            }
            else if (!string.IsNullOrEmpty(part))
            {
                // Strip any remaining angle-bracket tags in text segments.
                var stripped = Regex.Replace(part, @"<[^>]+>", string.Empty);
                sb.Append(WebUtility.HtmlEncode(stripped));
            }
        }

        return sb.ToString();
    }

    public static AdminSearchAutocompleteHitDto ToDto(AutocompleteHit hit)
    {
        var routeIds = hit.RouteParams?.Ids ?? new Google.Protobuf.Collections.MapField<string, string>();
        return new AdminSearchAutocompleteHitDto
        {
            EntityType = hit.DocumentType,
            EntityId = hit.EntityId,
            FaceId = string.IsNullOrWhiteSpace(hit.FaceId) ? null : hit.FaceId,
            Title = EncodePlainText(hit.Title),
            Subtitle = string.IsNullOrWhiteSpace(hit.Subtitle) ? null : EncodePlainText(hit.Subtitle),
            Highlights = hit.Highlights.Select(SanitizeHighlight).ToList(),
            RouteParams = new AdminSearchRouteParamsDto
            {
                Type = hit.RouteParams?.Type ?? hit.DocumentType,
                Ids = routeIds.ToDictionary(kv => kv.Key, kv => kv.Value),
            },
        };
    }
}
