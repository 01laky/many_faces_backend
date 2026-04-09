/*
 * FacesControllerTests.cs - Unit tests for FacesController
 * 
 * Tests all endpoints in FacesController:
 * - GET /api/faces - Get all faces
 * - GET /api/faces/{id} - Get face by ID
 * - POST /api/faces - Create new face
 * - PUT /api/faces/{id} - Update face
 * - DELETE /api/faces/{id} - Delete face
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for FacesController
/// </summary>
public class FacesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public FacesControllerTests(CustomWebApplicationFactory<Program> factory)
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

        var email = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Admin",
            lastName = "User"
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password
        };

        HttpResponseMessage? response = null;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        tokenResponse.Should().NotBeNull();
        _authToken = tokenResponse!.AccessToken;
        return _authToken;
    }

    [Fact]
    public async Task GetFaces_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        var response = await _client.GetAsync("/api/faces");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFaces_ShouldReturnFacesList_WhenAuthenticated()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/faces");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var faces = await response.Content.ReadFromJsonAsync<List<object>>();
        faces.Should().NotBeNull();
        faces!.Should().BeAssignableTo<IEnumerable<object>>();
    }

    [Fact]
    public async Task GetFace_ShouldReturnFace_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
            description = "Test Description"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createdFace.Should().NotBeNull();
        int faceId = (int)createdFace.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/faces/{faceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var face = await response.Content.ReadFromJsonAsync<JsonElement>();
        face.Should().NotBeNull();
        face.GetProperty("id").GetInt32().Should().Be(faceId);
    }

    [Fact]
    public async Task CreateFace_ShouldReturnCreated_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createRequest = new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
            description = "Test Description"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/faces", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var face = await response.Content.ReadFromJsonAsync<JsonElement>();
        face.Should().NotBeNull();
        face.GetProperty("title").GetString().Should().Be("Test Face");
        face.GetProperty("gradientSettings").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateFace_ShouldReturnBadRequest_WhenIndexExists()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var index = $"test_{Guid.NewGuid()}";

        // Create first face
        await _client.PostAsJsonAsync("/api/faces", new
        {
            index,
            title = "First Face"
        });

        // Try to create second face with same index
        var createRequest = new
        {
            index,
            title = "Second Face"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/faces", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateFace_ShouldReturnOk_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face"
        });

        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int faceId = (int)createdFace.GetProperty("id").GetInt32();

        var updateRequest = new
        {
            title = "Updated Face Title"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/faces/{faceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedFace = await response.Content.ReadFromJsonAsync<JsonElement>();
        updatedFace.Should().NotBeNull();
        updatedFace.GetProperty("title").GetString().Should().Be("Updated Face Title");
    }

    [Fact]
    public async Task DeleteFace_ShouldReturnNoContent_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face"
        });

        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int faceId = (int)createdFace.GetProperty("id").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/faces/{faceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
