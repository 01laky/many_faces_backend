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
		// Backend-refactor §2 (Security) hardening: normalize IPv4-mapped IPv6 (e.g. ::ffff:10.0.0.1) to its IPv4
		// form so the private-range checks below actually apply — a mapped address would otherwise slip the v4 guard.
		if (address.IsIPv4MappedToIPv6)
			address = address.MapToIPv4();

		if (IPAddress.IsLoopback(address))
			return true;
		if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
			return true;

		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			var bytes = address.GetAddressBytes();
			if (bytes[0] == 0)
				return true; // 0.0.0.0/8 "this network"
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
			if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
				return true; // 100.64/10 carrier-grade NAT
		}

		if (address.AddressFamily == AddressFamily.InterNetworkV6)
		{
			if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
				return true;
			var b = address.GetAddressBytes();
			if ((b[0] & 0xfe) == 0xfc)
				return true; // unique local address fc00::/7
		}

		// NOTE: this guards literal-IP SSRF only. DNS rebinding (a public hostname resolving to a private IP) is not
		// caught here — the fetching worker must validate the RESOLVED address at connect time (follow-up).
		return false;
	}
}
