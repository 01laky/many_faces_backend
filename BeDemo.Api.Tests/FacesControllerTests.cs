/*
 * FacesControllerTests.cs - Integration tests for FacesController
 *
 * Tenant scope (/public/...): list/read only the URL face; cannot create faces without global Admin + /admin/ scope.
 * Admin scope: integration seeded user (global Admin) performs CRUD.
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class FacesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _tenantToken;
    private HttpClient? _adminClient;

    public FacesControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GetTenantTokenAsync()
    {
        if (_tenantToken != null)
            return _tenantToken;

        var email = $"faces_tenant_{Guid.NewGuid()}@test.com";
        const string password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Face",
            lastName = "Tenant",
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password,
        };

        HttpResponseMessage? response = null;
        for (var i = 0; i < 15; i++)
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
        _tenantToken = tokenResponse!.AccessToken;
        return _tenantToken;
    }

    private async Task<HttpClient> GetAdminClientAsync()
    {
        if (_adminClient != null)
            return _adminClient;

        var c = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _adminClient = c;
        return _adminClient;
    }

    [Fact]
    public async Task GetFaces_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/faces");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFaces_ShouldReturnSingleScopedFace_WhenAuthenticatedOnTenant()
    {
        var token = await GetTenantTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/faces");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var faces = await response.Content.ReadFromJsonAsync<JsonElement>();
        faces!.ValueKind.Should().Be(JsonValueKind.Array);
        faces.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetFace_ShouldReturnFace_WhenValidId_AndAdminScope()
    {
        var admin = await GetAdminClientAsync();

        var createResponse = await admin.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
            description = "Test Description",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var faceId = createdFace!.GetProperty("id").GetInt32();

        var response = await admin.GetAsync($"/api/faces/{faceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var face = await response.Content.ReadFromJsonAsync<JsonElement>();
        face!.GetProperty("id").GetInt32().Should().Be(faceId);
    }

    [Fact]
    public async Task CreateFace_ShouldReturnForbid_OnTenantScope_WithoutGlobalAdmin()
    {
        var token = await GetTenantTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/faces", new
        {
            index = $"tenant_blocked_{Guid.NewGuid()}",
            title = "Nope",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateFace_ShouldReturnCreated_WhenGlobalAdminOnAdminScope()
    {
        var admin = await GetAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
            description = "Test Description",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var face = await response.Content.ReadFromJsonAsync<JsonElement>();
        face!.GetProperty("title").GetString().Should().Be("Test Face");
        face.GetProperty("gradientSettings").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateFace_ShouldReturnBadRequest_WhenIndexExists()
    {
        var admin = await GetAdminClientAsync();
        var index = $"test_{Guid.NewGuid()}";

        (await admin.PostAsJsonAsync("/api/faces", new { index, title = "First Face" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsJsonAsync("/api/faces", new { index, title = "Second Face" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateFace_ShouldReturnOk_WhenValidData()
    {
        var admin = await GetAdminClientAsync();

        var createResponse = await admin.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var faceId = createdFace!.GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync($"/api/faces/{faceId}", new { title = "Updated Face Title" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedFace = await response.Content.ReadFromJsonAsync<JsonElement>();
        updatedFace!.GetProperty("title").GetString().Should().Be("Updated Face Title");
    }

    [Fact]
    public async Task DeleteFace_ShouldReturnNoContent_WhenValidId()
    {
        var admin = await GetAdminClientAsync();

        var createResponse = await admin.PostAsJsonAsync("/api/faces", new
        {
            index = $"test_{Guid.NewGuid()}",
            title = "Test Face",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFace = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var faceId = createdFace!.GetProperty("id").GetInt32();

        var response = await admin.DeleteAsync($"/api/faces/{faceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client.Dispose();
        _adminClient?.Dispose();
    }
}
