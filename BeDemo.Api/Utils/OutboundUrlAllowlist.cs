using System.Net;
using System.Net.Sockets;

namespace BeDemo.Api.Utils;

/// <summary>
/// Validates outbound HTTPS URLs before the backend forwards them to workers (BSH3-G7 SSRF guard).
/// </summary>
public static class OutboundUrlAllowlist
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="url"/> is an absolute public HTTPS URL suitable for worker fetch.
    /// </summary>
    public static bool TryValidatePublicHttpsUrl(string? url, out string? rejectionReason)
    {
        rejectionReason = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            rejectionReason = "empty";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            rejectionReason = "invalid_uri";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "non_https";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            rejectionReason = "userinfo_forbidden";
            return false;
        }

        if (IsBlockedHost(uri.Host))
        {
            rejectionReason = "private_or_local_host";
            return false;
        }

        return true;
    }

    private static bool IsBlockedHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out var ip))
            return IsBlockedIp(ip);

        return false;
    }

    private static bool IsBlockedIp(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 10)
                return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            if (bytes[0] == 127)
                return true;
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
        }

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            return true;

        return false;
    }
}
