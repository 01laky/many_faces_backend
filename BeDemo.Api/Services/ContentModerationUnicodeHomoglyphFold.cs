using System.Globalization;

namespace BeDemo.Api.Services;

/// <summary>
/// Maps Cyrillic/Greek letters that visually resemble Latin ASCII to their Latin counterpart for heuristic scans only.
/// </summary>
/// <remarks>
/// <para>
/// Security hardening v2 <b>PI-6</b>: attackers smuggle <c>ignore previous</c> with homoglyphs (e.g. Cyrillic
/// <c>о</c> U+043E instead of Latin <c>o</c>) so naive substring checks miss. This fold applies only inside
/// <see cref="ContentModerationTextNormalization.BuildHeuristicScanBlob"/> — stored user content and gRPC wire
/// payloads are unchanged; legitimate Slovak/Czech copy that does not use lookalike letters is preserved.
/// </para>
/// <para>
/// This is not a full Unicode TR39 confusables implementation; extend the switch when red-team corpus grows.
/// </para>
/// </remarks>
internal static class ContentModerationUnicodeHomoglyphFold
{
	/// <summary>
	/// When true, maps the rune to a single lower-case Latin letter used for injection-pattern matching.
	/// </summary>
	/// <param name="codePoint">Unicode scalar after bidi/zero-width stripping.</param>
	/// <param name="latin">Lower-case ASCII a-z when mapped; otherwise default.</param>
	public static bool TryMapToLatinAscii(int codePoint, out char latin)
	{
		if (codePoint <= 0x7F)
		{
			if (codePoint is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
			{
				latin = char.ToLowerInvariant((char)codePoint);
				return true;
			}

			latin = default;
			return false;
		}

		latin = codePoint switch
		{
			// Cyrillic small letters commonly confused with Latin
			0x0430 => 'a', // а
			0x0435 => 'e', // е
			0x043E => 'o', // о
			0x0440 => 'p', // р
			0x0441 => 'c', // с
			0x0443 => 'y', // у
			0x0445 => 'x', // х
			0x0456 => 'i', // і (Ukrainian)
			0x0455 => 's', // ѕ (Macedonian; rare confusable)
			0x043C => 'm', // м
			0x043D => 'n', // н
			0x0442 => 't', // т
			0x0433 => 'g', // г
			0x0432 => 'v', // в
			0x043A => 'k', // к
			0x043B => 'l', // л
			0x0434 => 'd', // д
			0x0437 => 'z', // з
			0x0439 => 'j', // й (sometimes used in smuggled Latin phrases)
			0x04BB => 'h', // һ (Cyrillic shha, confusable with h)
						   // Cyrillic capital lookalikes (normalize to lower for scan blob)
			0x0410 => 'a',
			0x0415 => 'e',
			0x041E => 'o',
			0x0420 => 'p',
			0x0421 => 'c',
			0x0423 => 'y',
			0x0425 => 'x',
			0x0406 => 'i', // І Ukrainian
			0x041C => 'm',
			0x041D => 'n',
			0x0422 => 't',
			// Greek lookalikes
			0x03B1 => 'a', // α
			0x03B5 => 'e', // ε
			0x03BF => 'o', // ο
			0x03C1 => 'p', // ρ
			0x03C5 => 'u', // υ (sometimes scanned as u not y — close enough for "you" smuggling)
			0x03C7 => 'x', // χ
			_ => default,
		};

		return latin != default;
	}
}
