using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Security;

/// <summary>
/// Regression tests for the backend-refactor §2 SSRF hardening of <see cref="OutboundUrlAllowlist"/>: IPv4-mapped
/// IPv6, IPv6 ULA (fc00::/7), 0.0.0.0/8, CGNAT (100.64/10), and the IPv6 unspecified address are now blocked, while
/// genuine public hosts remain allowed.
/// </summary>
public sealed class OutboundUrlAllowlistHardeningTests
{
	[Theory]
	[InlineData("https://[::ffff:10.0.0.1]/x")]   // IPv4-mapped IPv6 of a private address
	[InlineData("https://[::ffff:127.0.0.1]/x")]  // mapped loopback
	[InlineData("https://[fc00::1]/x")]            // ULA
	[InlineData("https://[fd12:3456::1]/x")]       // ULA (fd00::/8 subset of fc00::/7)
	[InlineData("https://[::]/x")]                  // IPv6 unspecified
	[InlineData("https://0.0.0.0/x")]               // 0.0.0.0/8
	[InlineData("https://100.64.0.1/x")]            // CGNAT
	[InlineData("https://10.0.0.5/x")]              // existing private (still blocked)
	[InlineData("https://localhost/x")]
	public void Private_or_special_addresses_are_rejected(string url)
	{
		OutboundUrlAllowlist.TryValidatePublicHttpsUrl(url, out var reason).Should().BeFalse();
		reason.Should().NotBeNullOrEmpty();
	}

	[Theory]
	[InlineData("https://example.com/path")]
	[InlineData("https://8.8.8.8/x")]               // public IPv4 literal
	[InlineData("https://[2606:4700:4700::1111]/x")] // public IPv6 (Cloudflare)
	public void Genuine_public_https_urls_are_allowed(string url)
	{
		OutboundUrlAllowlist.TryValidatePublicHttpsUrl(url, out var reason).Should().BeTrue(reason);
	}
}
