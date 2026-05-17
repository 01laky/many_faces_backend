using System.Security.Cryptography;
using System.Text;

namespace BeDemo.Api.Utils;

/// <summary>
/// Helpers for structured logs that must not contain raw credentials, email addresses, or chat bodies (SHV2 BE-L1–L3).
/// </summary>
/// <remarks>
/// Use SHA-256 prefixes for correlation across Seq/Dozzle without storing reversible secrets.
/// Never log passwords, refresh tokens, registration codes, or full JWTs.
/// </remarks>
public static class PiiLogRedaction
{
    /// <summary>First 12 hex chars of UTF-8 SHA-256 — same width as moderation payload logging.</summary>
    public static string ComputeSha256Prefix(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "empty";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// OAuth password-grant identifier (username or email typed by user) — length + hash only.
    /// </summary>
    public static string FormatCredentialIdentifierForLog(string? usernameOrEmail)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
            return "credentialHint=empty";

        return $"credentialHintLength={usernameOrEmail.Length} credentialHintSha256Prefix={ComputeSha256Prefix(usernameOrEmail)}";
    }

    /// <summary>
    /// Registration / account email — invite id for ops plus domain suffix only (no local part).
    /// </summary>
    public static string FormatEmailForLog(string? email, Guid? inviteId = null)
    {
        var parts = new List<string>();
        if (inviteId.HasValue)
            parts.Add($"inviteId={inviteId.Value:N}");

        if (string.IsNullOrWhiteSpace(email))
        {
            parts.Add("emailDomain=missing");
            return string.Join(' ', parts);
        }

        var at = email.IndexOf('@');
        if (at > 0 && at < email.Length - 1)
            parts.Add($"emailDomain={email[(at + 1)..]}");
        else
            parts.Add($"emailSha256Prefix={ComputeSha256Prefix(email)}");

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Hub or AI user/operator message text — length + hash; never log raw prompt or chat content.
    /// </summary>
    public static string FormatChatMessageForLog(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "messageLength=0 messageSha256Prefix=empty";

        return $"messageLength={message.Length} messageSha256Prefix={ComputeSha256Prefix(message)}";
    }
}
