using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests;

/// <summary>Integration tests for FaceVideoLoungesController — VL-API-* edge cases from video-lounge-agent-prompt §13.</summary>
public class FaceVideoLoungesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string Password = "Test1234!@##";

    private readonly CustomWebApplicationFactory<Program> _factory;

    public FaceVideoLoungesControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<(string Token, string UserId)> LoginWithPasswordAsync(HttpClient client, string email, string password)
    {
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
            response = await client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        var token = tokenResponse!.AccessToken;
        var payload = token.Split('.')[1];
        var pad = payload.Length % 4 == 0 ? "" : new string('=', 4 - payload.Length % 4);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload + pad));
        var doc = JsonDocument.Parse(json);
        var userId = doc.RootElement.TryGetProperty("nameid", out var n) ? n.GetString() : doc.RootElement.GetProperty("sub").GetString();
        return (token, userId!);
    }

    private Task<(string Token, string UserId, string Email)> RegisterAndLoginAsync(HttpClient client) =>
        IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            client,
            _factory,
            $"vl_{Guid.NewGuid():N}@test.com",
            Password,
            "VL",
            "Tester");

    private static async Task EnableVideoLoungesCreateAsync(CustomWebApplicationFactory<Program> factory, int faceId, bool enabled)
    {
        using var admin = factory.CreateFaceClient("admin");
        var adminToken = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res = await admin.PutAsJsonAsync($"/api/faces/{faceId}", new { videoLoungesCreate = enabled });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string exactName)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        foreach (var r in roles!)
        {
            if (string.Equals(r.GetProperty("name").GetString(), exactName, StringComparison.Ordinal))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {exactName} not found");
    }

    private static async Task SetFaceUserRoleAsync(HttpClient client, string token, int faceId)
    {
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(HttpClient Client, string Token, string UserId, int FaceId, int LoungeId)> CreateMemberLoungeAsync()
    {
        var client = _factory.CreateClient();
        var (token, userId, _) = await RegisterAndLoginAsync(client);
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableVideoLoungesCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges", new
        {
            title = $"Lounge {Guid.NewGuid():N}",
            isPublic = true,
            maxParticipants = 4,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var loungeId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        return (client, token, userId, faceId, loungeId);
    }

    [Fact]
    public async Task VL_API_01_List_Should401_WithoutToken()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/faces/1/video-lounges")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VL_API_02_List_Should404_WhenFaceMissing()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/faces/999999/video-lounges")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VL_API_03_Create_Should403_WhenVideoLoungesCreateDisabled()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableVideoLoungesCreateAsync(_factory, faceId, false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges", new { title = "X", isPublic = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VL_API_04_Create_Should403_ForHost()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");
        await EnableVideoLoungesCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges", new { title = "Host", isPublic = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VL_API_11_LiveJoin_Should409_WhenNoSession()
    {
        var (client, token, _, faceId, loungeId) = await CreateMemberLoungeAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/join", new { joinMode = "Viewer" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task VL_API_07_LiveJoin_Should400_WhenJoinModeMissing()
    {
        var (client, token, _, faceId, loungeId) = await CreateMemberLoungeAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/start", null);
        (await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/join", new { joinMode = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VL_API_14_LiveJoin_Viewer_ShouldReturnStubToken()
    {
        var (client, token, _, faceId, loungeId) = await CreateMemberLoungeAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var join = await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/join", new { joinMode = "Viewer" });
        join.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await join.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("joinMode").GetString().Should().Be("Viewer");
        body.GetProperty("isStub").GetBoolean().Should().BeTrue();
        body.GetProperty("token").GetString().Should().StartWith("vl-stub.");
    }

    [Fact]
    public async Task VL_API_25_Roster_ShouldExcludeAdminStealth()
    {
        var (client, token, userId, faceId, loungeId) = await CreateMemberLoungeAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/start", null);
        await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/join", new { joinMode = "Full" });

        using var admin = _factory.CreateFaceClient("admin");
        var adminToken = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await admin.PostAsync($"/api/operator-content/video-lounges/{loungeId}/live/stealth-join", null);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var live = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/video-lounges/{loungeId}/live");
        live.GetProperty("liveParticipantCount").GetInt32().Should().Be(1);
        live.GetProperty("liveParticipants").GetArrayLength().Should().Be(1);
        live.GetProperty("liveParticipants")[0].GetProperty("userId").GetString().Should().Be(userId);
    }

    [Fact]
    public async Task VL_API_30_LiveStart_ShouldNotifyMembers()
    {
        var (client, aToken, _, faceId, loungeId) = await CreateMemberLoungeAsync();
        var (bToken, bUserId, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, bToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bToken);
        (await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/join", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aToken);
        (await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasNotification = await db.Notifications.AnyAsync(
            n => n.UserId == bUserId && n.Type == "video_lounge_live",
            CancellationToken.None);
        hasNotification.Should().BeTrue();
    }

    [Fact]
    public async Task VL_API_28_RefreshToken_Should200()
    {
        var (client, token, _, faceId, loungeId) = await CreateMemberLoungeAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/start", null);
        await client.PostAsJsonAsync($"/api/faces/{faceId}/video-lounges/{loungeId}/live/join", new { joinMode = "Listener" });
        var refresh = await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/video-lounges/{loungeId}/live/refresh-token",
            new { joinMode = "Listener" });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
