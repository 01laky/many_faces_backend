using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration tests for <see cref="Controllers.StatsController"/>: operator-only dashboard summary and timeseries.
/// Uses the same admin-face scoped client + integration admin token pattern as other platform-operator tests.
/// </summary>
public sealed class StatsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public StatsControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	public void Dispose() { }

	[Fact]
	public async Task GetStats_ShouldReturnUnauthorized_WhenNoToken()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.GetAsync("/api/Stats");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetStats_ShouldReturnForbidden_WhenTokenWithoutPlatformOperatorBar()
	{
		// Public face scope: even a valid JWT cannot satisfy CanManageAllFaces (requires admin face prefix).
		var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/Stats");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GetStats_ShouldReturnFullSummary_WhenAdminFaceScopedOperator()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/Stats");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("usersCount").GetInt32().Should().BeGreaterThan(0);
		json.GetProperty("facesCount").GetInt32().Should().BeGreaterThan(0);
		json.GetProperty("pagesCount").GetInt32().Should().BeGreaterThan(0);
		json.TryGetProperty("faceWallTicketsByStatus", out var wall).Should().BeTrue();
		wall.ValueKind.Should().Be(JsonValueKind.Object);
	}

	[Fact]
	public async Task GetPublicStats_ShouldReturnOk_WithoutAuth_OnPublicFaceScope()
	{
		var client = _factory.CreateFaceClient("public");
		var response = await client.GetAsync("/api/Stats/public");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("usersCount").GetInt32().Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task GetPublicStats_ShouldReturnUnauthorized_OnAdminFace_WithoutJwt()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.GetAsync("/api/Stats/public");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetPublicStats_ShouldReturnBadRequest_WhenBareApiPathMissingFacePrefix()
	{
		using var client = _factory.CreateUnscopedClient();
		var response = await client.GetAsync("/api/Stats/public");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GetPublicStats_ShouldReturnOk_OnPublicFace_WithOperatorJwt()
	{
		var client = _factory.CreateFaceClient("public");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/Stats/public");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GetPublicStats_ShouldExposeOnlyAggregateNumericFields_AndNoOperatorAuditFields()
	{
		var client = _factory.CreateFaceClient("public");
		var response = await client.GetAsync("/api/Stats/public");
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();

		foreach (var name in new[]
				 {
					 "usersCount", "facesCount", "pagesCount", "friendshipsCount", "friendRequestsPendingCount",
					 "messagesCount", "albumsCount", "blogsCount", "reelsCount", "storiesCount", "storyViewsCount",
					 "faceWallTicketsCount", "faceChatRoomsCount", "faceChatRoomMessagesCount",
				 })
		{
			json.TryGetProperty(name, out var p).Should().BeTrue($"{name} missing");
			p.ValueKind.Should().Be(JsonValueKind.Number, because: $"{name} must be a JSON number");
		}

		json.TryGetProperty("oauthClientsCount", out _).Should().BeFalse();
		json.TryGetProperty("contentModerationEventsCount", out _).Should().BeFalse();
		json.TryGetProperty("aiReviewJobsCount", out _).Should().BeFalse();
	}

	[Fact]
	public async Task GetTimeseries_ShouldReturnBadRequest_WhenInvalidRange()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var to = DateTime.UtcNow;
		var from = to.AddDays(1);
		var url = $"/api/Stats/timeseries?metric=users&fromUtc={Uri.EscapeDataString(from.ToString("o"))}&toUtc={Uri.EscapeDataString(to.ToString("o"))}";
		var response = await client.GetAsync(url);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GetTimeseries_ShouldReturnBuckets_WhenValidRequest()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var to = DateTime.UtcNow;
		var from = to.AddDays(-7);
		var url =
			$"/api/Stats/timeseries?metric=users&fromUtc={Uri.EscapeDataString(from.ToString("o"))}&toUtc={Uri.EscapeDataString(to.ToString("o"))}&bucket=day";
		var response = await client.GetAsync(url);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("metric").GetString().Should().Be("users");
		json.GetProperty("bucket").GetString().Should().Be("day");
		json.GetProperty("buckets").GetArrayLength().Should().BeGreaterThan(0);
	}
}
