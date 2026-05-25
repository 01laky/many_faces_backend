using System.Globalization;
using System.Text;

namespace BeDemo.Api.Services;

/// <summary>
/// Shared text normalization for moderation security (heuristic scans only — does not mutate stored user content).
/// </summary>
/// <remarks>
/// Security hardening v2 <b>PI-6</b>: combines bidi/zero-width stripping, percent-decoding, and
/// <see cref="ContentModerationUnicodeHomoglyphFold"/> so mixed-script and homoglyph smuggling still matches
/// instruction-like patterns.
/// </remarks>
public static class ContentModerationTextNormalization
{
	/// <summary>
	/// Builds a single lower-case blob from title, body, and media URL for pattern matching.
	/// Strips bidi / zero-width / format characters so smuggled <c>ignore previous</c> phrases still match.
	/// </summary>
	public static string BuildHeuristicScanBlob(string? title, string? body, string? mediaUrl)
	{
		// Decode percent-encoding in every field so query-string stuffing cannot hide phrases.
		var combined = string.Join(
			"\n",
			DecodePercentEncodingForScan(title),
			DecodePercentEncodingForScan(body),
			DecodePercentEncodingForScan(mediaUrl));

		if (string.IsNullOrWhiteSpace(combined))
			return string.Empty;

		var sb = new StringBuilder(combined.Length);
		foreach (var rune in combined.EnumerateRunes())
		{
			if (ContentModerationInputSanitizer.ShouldStripRuneForMatching(rune))
				continue;

			if (rune.Value is >= 0 and < 32 && rune.Value is not ('\n' or '\r' or '\t'))
				continue;

			// PI-6: fold Latin lookalikes (Cyrillic/Greek) before lowercasing so "ignоre" still matches "ignore".
			if (ContentModerationUnicodeHomoglyphFold.TryMapToLatinAscii(rune.Value, out var latin))
				sb.Append(latin);
			else
				sb.Append(rune);
		}

		return sb.ToString().ToLowerInvariant();
	}

	private static string DecodePercentEncodingForScan(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		try
		{
			return Uri.UnescapeDataString(value.Trim());
		}
		catch (UriFormatException)
		{
			return value.Trim();
		}
	}
}
