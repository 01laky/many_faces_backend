/*
 * UsersControllerTests.cs - Unit tests for UsersController
 * 
 * Tests all endpoints in UsersController:
 * - GET /api/users - Get all users
 * - GET /api/users/{id} - Get user by ID
 * - POST /api/users - Create new user
 * - PUT /api/users/{id} - Update user
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for UsersController
/// </summary>
public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;
	private string? _authToken;

	public UsersControllerTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	/// <summary>
	/// Helper method to authenticate and get JWT token
	/// </summary>
	private async Task<string> GetAuthTokenAsync()
	{
		if (_authToken != null)
			return _authToken;

		_authToken = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
			_client,
			_factory,
			$"admin_{Guid.NewGuid()}@test.com",
			"Test1234!@##",
			"Admin",
			"User");
		return _authToken;
	}

	private async Task<HttpClient> CreateAdminApiClientAsync()
	{
		var admin = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
		admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return admin;
	}

	[Fact]
	public async Task GetUsers_ShouldReturnUnauthorized_WhenNoToken()
	{
		// Act
		var response = await _client.GetAsync("/api/users");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetUsers_ShouldReturnUsersList_WhenAuthenticated()
	{
		// Arrange
		var token = await GetAuthTokenAsync();
		_client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

		// Act
		var response = await _client.GetAsync("/api/users");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.TryGetProperty("items", out var items).Should().BeTrue();
		items.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
		body.TryGetProperty("totalCount", out _).Should().BeTrue();
	}

	[Fact]
	public async Task GetUser_ShouldReturnUser_WhenValidId()
	{
		using var admin = await CreateAdminApiClientAsync();

		var createResponse = await admin.PostAsJsonAsync("/api/users", new
		{
			email = $"test_{Guid.NewGuid()}@test.com",
			password = "Test1234!@##",
			firstName = "Test",
			lastName = "User",
		});
		createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var createdUserJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var userId = createdUserJson.GetProperty("id").GetString() ?? string.Empty;

		var response = await admin.GetAsync($"/api/users/{userId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = await response.Content.ReadFromJsonAsync<JsonElement>();
		user!.ValueKind.Should().Be(JsonValueKind.Object);
		user.GetProperty("id").GetString().Should().Be(userId);
	}

	[Fact]
	public async Task GetUser_ShouldReturnNotFound_WhenInvalidId()
	{
		// Arrange
		var token = await GetAuthTokenAsync();
		_client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

		// Act
		var response = await _client.GetAsync("/api/users/invalid-id");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CreateUser_ShouldReturnCreated_WhenValidData()
	{
		using var admin = await CreateAdminApiClientAsync();

		var createRequest = new
		{
			email = $"test_{Guid.NewGuid()}@test.com",
			password = "Test1234!@##",
			firstName = "Test",
			lastName = "User",
		};

		var response = await admin.PostAsJsonAsync("/api/users", createRequest);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var user = await response.Content.ReadFromJsonAsync<JsonElement>();
		user!.ValueKind.Should().Be(JsonValueKind.Object);
		user.GetProperty("email").GetString().Should().Be(createRequest.email);
	}

	[Fact]
	public async Task CreateUser_ShouldReturnBadRequest_WhenInvalidEmail()
	{
		// Validation is only reached by an authorized (super-admin) caller — the ManageAllFaces policy runs first, so an
		// unauthorized caller would be 403'd before model binding. Use the admin client to exercise the 400 path.
		using var admin = await CreateAdminApiClientAsync();

		var createRequest = new
		{
			email = "invalid-email",
			password = "Test1234!@##",
			firstName = "Test",
			lastName = "User",
		};

		var response = await admin.PostAsJsonAsync("/api/users", createRequest);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task UpdateUser_ShouldReturnOk_WhenValidData()
	{
		using var admin = await CreateAdminApiClientAsync();

		var createResponse = await admin.PostAsJsonAsync("/api/users", new
		{
			email = $"test_{Guid.NewGuid()}@test.com",
			password = "Test1234!@##",
			firstName = "Test",
			lastName = "User",
		});
		createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var userId = createdUser.GetProperty("id").GetString() ?? string.Empty;

		var updateRequest = new
		{
			firstName = "Updated",
			lastName = "Name",
		};

		var response = await admin.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var updatedUser = await response.Content.ReadFromJsonAsync<JsonElement>();
		updatedUser.ValueKind.Should().Be(JsonValueKind.Object);
		updatedUser.GetProperty("firstName").GetString().Should().Be("Updated");
	}

	public void Dispose()
	{
		_client?.Dispose();
	}
}
