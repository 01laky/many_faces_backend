using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class ReelsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public ReelsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null)
            return _authToken;

        var email = $"reel_test_{Guid.NewGuid()}@test.com";
        const string password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Reel",
            lastName = "Tester",
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

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        _authToken = tokenResponse!.AccessToken;
        return _authToken;
    }

    private static void SetAuth(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<int> GetScopedFaceIdAsync(HttpClient faceClient, string token)
    {
        SetAuth(faceClient, token);
        var cfg = await faceClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        cfg.Should().NotBeNull();
        cfg!.Length.Should().Be(1);
        return cfg[0].GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTestReelAsync(HttpClient client, List<int>? faceIds = null, string? videoUrl = null)
    {
        var token = await GetAuthTokenAsync();
        SetAuth(client, token);

        var resp = await client.PostAsJsonAsync("/api/reels", new
        {
            title = $"Reel {Guid.NewGuid()}",
            description = "Test",
            videoUrl = videoUrl ?? "https://example.com/video.mp4",
            faceIds,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var reel = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return reel!.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetReels_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/reels");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReels_ShouldReturnList_WhenAuthenticated()
    {
        SetAuth(_client, await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/reels");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reels = await response.Content.ReadFromJsonAsync<JsonElement>();
        reels!.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CreateReel_ShouldReturnCreated_WithValidData()
    {
        SetAuth(_client, await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = "My Reel",
            description = "Desc",
            videoUrl = "https://cdn.example.com/v.mp4",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reel = await response.Content.ReadFromJsonAsync<JsonElement>();
        reel!.GetProperty("title").GetString().Should().Be("My Reel");
        reel.GetProperty("videoUrl").GetString().Should().Be("https://cdn.example.com/v.mp4");
    }

    [Fact]
    public async Task CreateReel_ShouldReturnBadRequest_WhenVideoUrlMissing()
    {
        SetAuth(_client, await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = "No video",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetReels_ShouldFilterByScopedFace_WhenReelLinkedToOneTenant()
    {
        var token = await GetAuthTokenAsync();
        using var publicClient = _factory.CreateFaceClient("public");
        using var basicClient = _factory.CreateFaceClient("basic");
        var publicFaceId = await GetScopedFaceIdAsync(publicClient, token);

        var scopedId = await CreateTestReelAsync(publicClient, new List<int> { publicFaceId });
        await CreateTestReelAsync(publicClient, null);

        var onPublic = await publicClient.GetFromJsonAsync<JsonElement[]>("/api/reels");
        onPublic.Should().NotBeNull();
        onPublic!.Select(e => e.GetProperty("id").GetInt32()).Should().Contain(scopedId);

        SetAuth(basicClient, token);
        var onBasic = await basicClient.GetFromJsonAsync<JsonElement[]>("/api/reels");
        onBasic.Should().NotBeNull();
        onBasic!.Select(e => e.GetProperty("id").GetInt32()).Should().NotContain(scopedId);
    }

    [Fact]
    public async Task GetReel_ScopedWithoutFaceId_ShouldReturnNotFound_WhenReelNotOnUrlTenant()
    {
        var token = await GetAuthTokenAsync();
        using var basicClient = _factory.CreateFaceClient("basic");
        using var publicClient = _factory.CreateFaceClient("public");
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token);
        var reelId = await CreateTestReelAsync(basicClient, new List<int> { basicFaceId });

        SetAuth(publicClient, token);
        var response = await publicClient.GetAsync($"/api/reels/{reelId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReel_ScopedWithMatchingUrlTenant_ShouldReturnOk()
    {
        var token = await GetAuthTokenAsync();
        using var basicClient = _factory.CreateFaceClient("basic");
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token);
        var reelId = await CreateTestReelAsync(basicClient, new List<int> { basicFaceId });

        var response = await basicClient.GetAsync($"/api/reels/{reelId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReelComments_Should404_WhenReelNotVisibleOnUrlTenant()
    {
        var token = await GetAuthTokenAsync();
        using var basicClient = _factory.CreateFaceClient("basic");
        using var publicClient = _factory.CreateFaceClient("public");
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token);
        var reelId = await CreateTestReelAsync(basicClient, new List<int> { basicFaceId });

        SetAuth(publicClient, token);
        var noFace = await publicClient.GetAsync($"/api/reels/{reelId}/comments");
        noFace.StatusCode.Should().Be(HttpStatusCode.NotFound);

        SetAuth(basicClient, token);
        var ok = await basicClient.GetAsync($"/api/reels/{reelId}/comments");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReelLike_ShouldWork_WhenVisible()
    {
        var token = await GetAuthTokenAsync();
        using var basicClient = _factory.CreateFaceClient("basic");
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token);
        var reelId = await CreateTestReelAsync(basicClient, new List<int> { basicFaceId });

        SetAuth(basicClient, token);
        var like = await basicClient.PostAsync($"/api/reels/{reelId}/likes", null);
        like.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteReel_ShouldReturnNoContent_WhenCreator()
    {
        var reelId = await CreateTestReelAsync(_client);

        var response = await _client.DeleteAsync($"/api/reels/{reelId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose() => _client.Dispose();
}
