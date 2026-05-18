using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models;
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

        _authToken = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            _client,
            _factory,
            $"reel_test_{Guid.NewGuid()}@test.com",
            "Test1234!@##",
            "Reel",
            "Tester");
        return _authToken;
    }

    private static void SetAuth(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<int> GetScopedFaceIdAsync(HttpClient faceClient, string token, string scopedFaceIndex)
    {
        SetAuth(faceClient, token);
        return await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(faceClient, token, scopedFaceIndex);
    }

    private async Task ApproveAsSuperAdminAsync(ModeratedContentType contentType, int contentId)
    {
        using var admin = _factory.CreateFaceClient("admin");
        SetAuth(admin, await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin));
        var response = await admin.PostAsJsonAsync(
            $"/api/contentmoderation/{contentType}/{contentId}/approve",
            new { reason = "Approved for test" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        reel.GetProperty("approvalStatus").GetString().Should().Be(nameof(ContentApprovalStatus.PendingApproval));
        reel.GetProperty("aiReviewStatus").GetString().Should().Be(nameof(AiReviewStatus.Queued));
    }

    [Fact]
    public async Task CreateReel_ShouldRejectUnsafeVideoUrl()
    {
        SetAuth(_client, await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = "Unsafe",
            videoUrl = "javascript:alert(1)",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateReel_ShouldNotAppearInPublicList_UntilApproved()
    {
        var reelId = await CreateTestReelAsync(_client);

        var list = await _client.GetFromJsonAsync<JsonElement[]>("/api/reels");
        list!.Select(e => e.GetProperty("id").GetInt32()).Should().NotContain(reelId);

        await ApproveAsSuperAdminAsync(ModeratedContentType.Reel, reelId);

        var approvedList = await _client.GetFromJsonAsync<JsonElement[]>("/api/reels");
        approvedList!.Select(e => e.GetProperty("id").GetInt32()).Should().Contain(reelId);
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
        var publicFaceId = await GetScopedFaceIdAsync(publicClient, token, "public");

        var scopedId = await CreateTestReelAsync(publicClient, new List<int> { publicFaceId });
        await CreateTestReelAsync(publicClient, null);
        await ApproveAsSuperAdminAsync(ModeratedContentType.Reel, scopedId);

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
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token, "basic");
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
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token, "basic");
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
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token, "basic");
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
        var basicFaceId = await GetScopedFaceIdAsync(basicClient, token, "basic");
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
