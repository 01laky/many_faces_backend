/*
 * PagesControllerTests.cs - Unit tests for PagesController
 * 
 * Tests all endpoints in PagesController:
 * - GET /api/pages - Get all pages (optionally filtered by faceId)
 * - GET /api/pages/{id} - Get page by ID
 * - POST /api/pages - Create new page
 * - PUT /api/pages/{id} - Update page
 * - DELETE /api/pages/{id} - Delete page
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
/// Unit tests for PagesController
/// </summary>
public class PagesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _oauth;
    private readonly HttpClient _adminFace;
    private string? _authToken;

    public PagesControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _oauth = AclTestClients.CreateOAuthClient(factory);
        _adminFace = AclTestClients.CreateAdminFaceClient(factory);
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

    /// <summary>Face id for the current URL scope (tests use the default <c>public</c> client).</summary>
    private async Task<int> GetScopedFaceIdAsync()
    {
        var token = await GetAuthTokenAsync();
        return await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, token, "public");
    }

    /// <summary>
    /// Helper method to create a PageType for testing
    /// </summary>
    private async Task<int> CreateTestPageTypeAsync()
    {
        var adminToken = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
        _adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var pageTypeResponse = await _adminFace.PostAsJsonAsync("/api/pagetypes", new
        {
            index = $"test_{Guid.NewGuid()}"
        });

        pageTypeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var pageType = await pageTypeResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (int)pageType.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetPages_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        var response = await _client.GetAsync("/api/pages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPages_ShouldReturnPagesList_WhenAuthenticated()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/pages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.Should().NotBeNull();
        body!.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetPage_ShouldReturnPage_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var faceId = await GetScopedFaceIdAsync();
        var pageTypeId = await CreateTestPageTypeAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/pages", new
        {
            faceId,
            pageTypeId,
            name = "Test Page",
            path = "/test",
            index = 0
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPage = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createdPage.Should().NotBeNull();
        int pageId = (int)createdPage.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/pages/{pageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        page.Should().NotBeNull();
        page.GetProperty("id").GetInt32().Should().Be(pageId);
    }

    [Fact]
    public async Task CreatePage_ShouldReturnCreated_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var faceId = await GetScopedFaceIdAsync();
        var pageTypeId = await CreateTestPageTypeAsync();

        var createRequest = new
        {
            faceId,
            pageTypeId,
            name = "Test Page",
            path = "/test",
            index = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pages", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        page.Should().NotBeNull();
        page.GetProperty("name").GetString().Should().Be("Test Page");
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequest_WhenFaceNotFound()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var pageTypeId = await CreateTestPageTypeAsync();

        var createRequest = new
        {
            faceId = 99999, // Non-existent face ID
            pageTypeId,
            name = "Test Page",
            path = "/test",
            index = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pages", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnOk_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var faceId = await GetScopedFaceIdAsync();
        var pageTypeId = await CreateTestPageTypeAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/pages", new
        {
            faceId,
            pageTypeId,
            name = "Test Page",
            path = "/test",
            index = 0
        });

        var createdPage = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int pageId = (int)createdPage.GetProperty("id").GetInt32();

        var updateRequest = new
        {
            name = "Updated Page Name"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/pages/{pageId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedPage = await response.Content.ReadFromJsonAsync<JsonElement>();
        updatedPage.Should().NotBeNull();
        updatedPage.GetProperty("name").GetString().Should().Be("Updated Page Name");
    }

    [Fact]
    public async Task DeletePage_ShouldReturnNoContent_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var faceId = await GetScopedFaceIdAsync();
        var pageTypeId = await CreateTestPageTypeAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/pages", new
        {
            faceId,
            pageTypeId,
            name = "Test Page",
            path = "/test",
            index = 0
        });

        var createdPage = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int pageId = (int)createdPage.GetProperty("id").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/pages/{pageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _oauth?.Dispose();
        _adminFace?.Dispose();
    }
}
