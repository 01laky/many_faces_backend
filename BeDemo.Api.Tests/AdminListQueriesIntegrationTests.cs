using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Integration coverage for admin list query validation and pagination clamp (§7.1 B-T2, B-T9, B-T10).</summary>
public sealed class AdminListQueriesIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _adminClient;

	public AdminListQueriesIntegrationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_adminClient = factory.CreateFaceClient("admin");
	}

	private async Task AuthorizeAdminAsync()
	{
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(_adminClient);
		_adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
	}

	[Fact]
	public async Task GetUsers_invalid_sortBy_returns_400_with_problem_details()
	{
		await AuthorizeAdminAsync();

		var response = await _adminClient.GetAsync("/api/users?sortBy=notAllowed&sortDir=asc");

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.TryGetProperty("errors", out var errors).Should().BeTrue();
		(errors.TryGetProperty("sortBy", out _) || errors.TryGetProperty("SortBy", out _)).Should().BeTrue();
	}

	[Fact]
	public async Task GetFaces_page_beyond_total_is_clamped_in_response()
	{
		await AuthorizeAdminAsync();

		var response = await _adminClient.GetAsync("/api/faces?page=999&pageSize=10");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		var page = body.GetProperty("page").GetInt32();
		var totalPages = body.GetProperty("totalPages").GetInt32();
		page.Should().BeLessThanOrEqualTo(Math.Max(1, totalPages));
	}

	[Fact]
	public async Task GetFaces_tenant_scope_returns_single_face_envelope()
	{
		var client = _factory.CreateClient();
		var email = $"list_tenant_{Guid.NewGuid()}@test.com";
		var token = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
			client,
			_factory,
			email,
			"Test1234!@##",
			"List",
			"Tenant");
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/faces?page=1&pageSize=10");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("items").GetArrayLength().Should().Be(1);
		body.GetProperty("totalCount").GetInt32().Should().Be(1);
		client.Dispose();
	}

	public void Dispose() => _adminClient.Dispose();
}
