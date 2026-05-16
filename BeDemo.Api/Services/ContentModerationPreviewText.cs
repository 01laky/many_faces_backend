using System.Net;
using System.Text.RegularExpressions;

namespace BeDemo.Api.Services;

/// <summary>
/// SHV2 <b>PI-8</b>: builds operator-safe plain-text previews from untrusted creator HTML/text for admin moderation UI.
/// </summary>
/// <remarks>
/// Never returns raw HTML for embedding via <c>dangerouslySetInnerHTML</c>. Strips tags, decodes common entities,
/// collapses whitespace, and caps length so queue/detail views cannot execute script or smuggle bidi controls into DOM.
/// </remarks>
public static partial class ContentModerationPreviewText
{
    /// <summary>Maximum characters returned on moderation queue/detail APIs.</summary>
    public const int MaxPreviewLength = 4_000;

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// Converts stored blog HTML or album/reel description into a single-line-safe plain preview string.
    /// </summary>
    public static string ToPlainTextPreview(string? htmlOrText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
            return string.Empty;

        var withoutTags = HtmlTagRegex().Replace(htmlOrText, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var collapsed = string.Join(
            ' ',
            decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (collapsed.Length <= MaxPreviewLength)
            return collapsed;

        return string.Concat(collapsed.AsSpan(0, MaxPreviewLength), "…");
    }

    /// <summary>Returns a length-capped media URL string for operator preview (no HTML interpretation).</summary>
    public static string? ToMediaUrlPreview(string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            return null;

        var trimmed = mediaUrl.Trim();
        return trimmed.Length <= ContentModerationInputSanitizer.MaxMediaUrlLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, ContentModerationInputSanitizer.MaxMediaUrlLength), "…");
    }
}
