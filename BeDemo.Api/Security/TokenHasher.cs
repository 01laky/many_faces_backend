using System.Security.Cryptography;
using System.Text;

namespace BeDemo.Api.Security;

/// <summary>
/// Stable hashing for opaque refresh tokens — never log or persist the plaintext beyond the wire to the client.
/// </summary>
public static class TokenHasher
{
	public static string Sha256Hex(string plaintext)
	{
		var bytes = Encoding.UTF8.GetBytes(plaintext);
		var hash = SHA256.HashData(bytes);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
