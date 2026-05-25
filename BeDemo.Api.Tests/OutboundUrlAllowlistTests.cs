using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

[Trait("Category", "BackendSecurity")]
public sealed class OutboundUrlAllowlistTests
{
	[Theory]
	[InlineData("https://example.com/api/stats/public")]
	[InlineData("https://stats.example.org/v1/data")]
	public void TryValidate_accepts_public_https(string url)
	{
		OutboundUrlAllowlist.TryValidatePublicHttpsUrl(url, out var reason).Should().BeTrue();
		reason.Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("http://example.com/stats")]
	[InlineData("https://localhost/stats")]
	[InlineData("https://127.0.0.1/stats")]
	[InlineData("https://10.0.0.1/stats")]
	[InlineData("https://192.168.1.1/stats")]
	[InlineData("not-a-url")]
	public void TryValidate_rejects_unsafe_urls(string? url)
	{
		OutboundUrlAllowlist.TryValidatePublicHttpsUrl(url, out var reason).Should().BeFalse();
		reason.Should().NotBeNullOrWhiteSpace();
	}
}
