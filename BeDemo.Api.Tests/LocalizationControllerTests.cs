using System.Net;
using System.Text.Json;

namespace BeDemo.Api.Tests;

/// <summary>
/// Happy-path and routing tests for <c>GET /api/localization/{app}</c> (face-prefix exempt, anonymous).
/// </summary>
/// <remarks>
/// Rate-limit rejection (429 + <c>Retry-After</c>) is covered by <see cref="LocalizationRateLimit429Tests"/>.
/// Golden-file regression for portal auth copy (legacy <c>en.json</c> subtree) is in <see cref="LocalizationPortalGoldenTests"/>.
/// </remarks>
public class LocalizationControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly HttpClient _client;

	public LocalizationControllerTests(CustomWebApplicationFactory<Program> factory)
	{
		_client = factory.CreateClient();
	}

	[Theory]
	[InlineData("portal")]
	[InlineData("admin")]
	[InlineData("mobile")]
	public async Task GetBundle_WithoutFacePrefix_Returns200(string app)
	{
		var response = await _client.GetAsync($"/api/localization/{app}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var json = await response.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		Assert.Equal(app, doc.RootElement.GetProperty("app").GetString());
		Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("version").GetString()));
		var resources = doc.RootElement.GetProperty("resources");
		Assert.True(resources.TryGetProperty("en", out _));
		Assert.True(resources.TryGetProperty("sk", out _));
		Assert.True(resources.TryGetProperty("cz", out _));
	}

	[Fact]
	public async Task GetBundle_UnknownApp_Returns404()
	{
		var response = await _client.GetAsync("/api/localization/unknown");
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task GetBundle_Portal_HasLoginTitle()
	{
		var response = await _client.GetAsync("/api/localization/portal");
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		var title = doc.RootElement
			.GetProperty("resources")
			.GetProperty("en")
			.GetProperty("common")
			.GetProperty("pages")
			.GetProperty("login")
			.GetProperty("title")
			.GetString();
		Assert.Equal("Login", title);
	}
}
