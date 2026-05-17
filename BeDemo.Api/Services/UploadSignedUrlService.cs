using System.Security.Cryptography;
using System.Text;
using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// HMAC-SHA256 signed URLs for upload downloads (SHV2 BE-U3). Replaces anonymous <c>UseStaticFiles</c> on <c>/uploads/*</c>.
/// </summary>
public sealed class UploadSignedUrlService : IUploadSignedUrlService
{
    private readonly byte[] _signingKey;
    private readonly int _lifetimeMinutes;

    public UploadSignedUrlService(IOptions<UploadsOptions> options)
    {
        var secret = options.Value.SigningSecret;
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException($"Uploads:{nameof(UploadsOptions.SigningSecret)} must be configured.");

        _signingKey = Encoding.UTF8.GetBytes(secret);
        _lifetimeMinutes = options.Value.SignedUrlLifetimeMinutes > 0
            ? options.Value.SignedUrlLifetimeMinutes
            : 60;
    }

    /// <inheritdoc />
    public string? ToAbsoluteSignedUrl(string? storedPath, string scheme, string host)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        if (storedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return storedPath;

        var relative = ToSignedServePath(storedPath);
        return relative == null ? null : $"{scheme}://{host}{relative}";
    }

    /// <inheritdoc />
    public string? ToSignedServePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        var normalized = NormalizeStoredUploadPath(storedPath);
        if (normalized == null)
            return null;

        var expires = DateTimeOffset.UtcNow.AddMinutes(_lifetimeMinutes).ToUnixTimeSeconds();
        var sig = ComputeSignature(normalized, expires);
        return $"/api/uploads/serve?path={Uri.EscapeDataString(normalized)}&exp={expires}&sig={Uri.EscapeDataString(sig)}";
    }

    /// <inheritdoc />
    public bool TryValidateServeRequest(string path, long expiresUnix, string signature, out string? storedPath, out string? error)
    {
        storedPath = null;
        error = null;

        var normalized = NormalizeStoredUploadPath(path);
        if (normalized == null)
        {
            error = "Invalid path";
            return false;
        }

        if (expiresUnix <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            error = "URL expired";
            return false;
        }

        var expected = ComputeSignature(normalized, expiresUnix);
        if (!FixedTimeEqualsSignature(expected, signature))
        {
            error = "Invalid signature";
            return false;
        }

        storedPath = normalized;
        return true;
    }

    /// <summary>Only paths under <c>/uploads/</c> may be signed or served.</summary>
    internal static string? NormalizeStoredUploadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var p = path.Trim().Replace('\\', '/');
        if (!p.StartsWith('/'))
            p = "/" + p;

        if (!p.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        if (p.Contains("..", StringComparison.Ordinal))
            return null;

        return p;
    }

    private static bool FixedTimeEqualsSignature(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
            return false;
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(actual);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private string ComputeSignature(string normalizedPath, long expiresUnix)
    {
        var payload = $"{normalizedPath}\n{expiresUnix}";
        using var hmac = new HMACSHA256(_signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
