using System.Net;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Asserts baseline security headers (H1) on an anonymous JSON endpoint used by JWKS discovery.
/// </summary>
[Trait("Category", "BackendSecurity")]
public sealed class SecurityHeadersIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public SecurityHeadersIntegrationTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Jwks_Response_Includes_SecurityHeaders()
	{
		using var client = _factory.CreateUnscopedClient();
		using var response = await client.GetAsync("/api/oauth2/jwks");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
		string.Join(",", nosniff!).Should().Contain("nosniff");
		response.Headers.TryGetValues("X-Frame-Options", out var xfo).Should().BeTrue();
		string.Join(",", xfo!).Should().Contain("DENY");
		response.Headers.TryGetValues("Content-Security-Policy", out var csp).Should().BeTrue();
		string.Join(",", csp!).Should().Contain("default-src");
	}
}
