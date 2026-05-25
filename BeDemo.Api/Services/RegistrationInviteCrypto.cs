using System.Security.Cryptography;
using System.Text;

namespace BeDemo.Api.Services;

/// <summary>
/// Cryptographic helpers for the email-code registration flow.
/// Separates concerns from <see cref="RegistrationInviteService"/>:
/// human-readable codes, opaque URL hashes, and peppered HMAC storage for codes.
/// </summary>
internal static class RegistrationInviteCrypto
{
	/// <summary>
	/// Alphabet without ambiguous glyphs (0/O, 1/I) so users can copy codes from email reliably.
	/// </summary>
	private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

	/// <summary>
	/// Builds a random verification code shown in the email body (never stored in plaintext in the DB).
	/// </summary>
	public static string GenerateVerificationCode(int length)
	{
		var bytes = new byte[length];
		RandomNumberGenerator.Fill(bytes);
		var chars = new char[length];
		for (var i = 0; i < length; i++)
		{
			// Map each random byte into the safe alphabet (modulo bias is acceptable at this length).
			chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
		}

		return new string(chars);
	}

	/// <summary>
	/// Creates the opaque <c>?hash=</c> query value: 32 bytes of entropy, URL-safe Base64 (no padding).
	/// This value is stored as-is in <see cref="Models.RegistrationInvite.LinkHash"/> for direct lookup.
	/// </summary>
	public static string GenerateLinkHash()
	{
		var bytes = new byte[32];
		RandomNumberGenerator.Fill(bytes);
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	/// <summary>
	/// Derives a stored fingerprint for the human code using server pepper (HMAC-SHA256, hex output).
	/// Codes are normalized to uppercase before hashing so user input is case-insensitive.
	/// </summary>
	public static string HashCode(string plainCode, string pepper)
	{
		var key = Encoding.UTF8.GetBytes(pepper);
		var data = Encoding.UTF8.GetBytes(plainCode.Trim().ToUpperInvariant());
		using var hmac = new HMACSHA256(key);
		return Convert.ToHexString(hmac.ComputeHash(data));
	}

	/// <summary>
	/// Compares two hex digests in constant time to reduce timing side channels on wrong-code attempts.
	/// </summary>
	public static bool FixedTimeEqualsHash(string expectedHex, string actualHex)
	{
		if (string.IsNullOrEmpty(expectedHex) || string.IsNullOrEmpty(actualHex))
		{
			return false;
		}

		try
		{
			var a = Convert.FromHexString(expectedHex);
			var b = Convert.FromHexString(actualHex);
			return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
