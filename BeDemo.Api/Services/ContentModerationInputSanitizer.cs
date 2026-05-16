using System.Globalization;
using System.Text;

namespace BeDemo.Api.Services;

/// <summary>
/// Normalizes untrusted album/blog/reel text before it is sent to <c>ReviewContent</c> (gRPC).
/// Complements in-process defenses in <c>many_faces_ai</c>; never weakens backend <see cref="ContentModerationHelpers.ValidateRecommendation"/>.
/// </summary>
public static class ContentModerationInputSanitizer
{
    /// <summary>Matches EF <c>Blog.Title</c> / album &amp; reel titles.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Hard cap for body/description sent to the AI channel (blog HTML can be unbounded in DB).</summary>
    public const int MaxBodyLengthForAi = 100_000;

    /// <summary>Above reel <c>VideoUrl</c> column and safe headroom for query strings.</summary>
    public const int MaxMediaUrlLength = 2000;

    /// <summary>Returns trimmed, control-stripped, length-capped strings safe to embed in moderation prompts.</summary>
    public static (string Title, string Body, string? MediaUrl) SanitizeForAiReview(
        string? title,
        string? body,
        string? mediaUrl)
    {
        var t = TrimAndStripControls(title, MaxTitleLength);
        var b = TrimAndStripControls(body, MaxBodyLengthForAi);
        string? m = null;
        if (!string.IsNullOrWhiteSpace(mediaUrl))
        {
            var trimmed = mediaUrl.Trim();
            m = TrimAndStripControls(trimmed, MaxMediaUrlLength);
            if (m.Length == 0)
                m = null;
        }

        return (t, b, m);
    }

    private static string TrimAndStripControls(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(Math.Min(value.Length, maxLength + 64));
        var len = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (len >= maxLength)
                break;

            if (ShouldStripRuneForMatching(rune))
                continue;

            if (rune.Value is >= 0 and < 32 && rune.Value is not ('\n' or '\r' or '\t'))
                continue;

            sb.Append(rune);
            len++;
        }

        return sb.ToString().Trim();
    }

    /// <summary>Bidi / format characters often used to spoof delimiters or confuse parsers (also used by heuristic scans).</summary>
    internal static bool ShouldStripRuneForMatching(Rune rune)
    {
        var v = rune.Value;
        return v is
            0x061C or // ARABIC LETTER MARK
            0x200B or // ZERO WIDTH SPACE
            0x200C or // ZERO WIDTH NON-JOINER
            0x200D or // ZERO WIDTH JOINER
            0x200E or // LRM
            0x200F or // RLM
            0x202A or // LRE
            0x202B or // RLE
            0x202C or // PDF
            0x202D or // LRO
            0x202E or // RLO
            0x2066 or // LRI
            0x2067 or // RLI
            0x2068 or // FSI
            0x2069 or // PDI
            0xFEFF; // BOM / ZWNBSP
    }
}
