using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// JWKS publication for JWT signature verification (GET /api/oauth2/jwks).
/// </summary>
public class OAuthJwksTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public OAuthJwksTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Jwks_Returns200_WithKeysArray()
	{
		var client = _factory.CreateClient();
		var response = await client.GetAsync("/api/oauth2/jwks");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("\"keys\"", "JWKS must expose a keys array");
		using var doc = JsonDocument.Parse(body);
		doc.RootElement.GetProperty("keys").GetArrayLength().Should().BeGreaterThan(0);
	}
}
