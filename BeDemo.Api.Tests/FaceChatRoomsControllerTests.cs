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

/// <summary>
/// Integration tests for FaceChatRoomsController — auth, host/member edges, system rooms, join flow.
/// </summary>
public class FaceChatRoomsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string ChatRoomsTestPassword = "Test1234!@##";

    private readonly CustomWebApplicationFactory<Program> _factory;

    public FaceChatRoomsControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

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
        userId.Should().NotBeNullOrEmpty();
        return (token, userId!);
    }

    private Task<(string Token, string UserId, string Email)> RegisterAndLoginAsync(HttpClient client) =>
        IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            client,
            _factory,
            $"cr_{Guid.NewGuid():N}@test.com",
            ChatRoomsTestPassword,
            "Chat",
            "Tester");

    /// <summary>
    /// Promotes user to global Admin in DB (API checks DB for system-room actions). J6: changing <see cref="ApplicationUser.UserRoleId"/> bumps
    /// <see cref="ApplicationUser.AccessTokenVersion"/> — returns a new access JWT for the same email/password.
    /// </summary>
    private static async Task<string> PromoteUserToGlobalAdminAsync(
        CustomWebApplicationFactory<Program> factory,
        HttpClient client,
        string userId,
        string userEmail)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.UserRoleId = adminRole.Id;
        await db.SaveChangesAsync();

        var (token, _) = await LoginWithPasswordAsync(client, userEmail, ChatRoomsTestPassword);
        return token;
    }

    private static async Task<int> GetAnyFaceIdAsync(HttpClient client, string token) =>
        await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string exactName)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        roles.Should().NotBeNull();
        foreach (var r in roles!)
        {
            var name = r.GetProperty("name").GetString() ?? "";
            if (string.Equals(name, exactName, StringComparison.Ordinal))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {exactName} not found");
    }

    /// <summary>Non-host participation so chat create / join are allowed by role checks.</summary>
    private static async Task SetFaceUserRoleAsync(HttpClient client, string token, int faceId)
    {
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Face flags like <c>ChatRoomsCreate</c> require global Admin on <c>/admin/</c> scope.</summary>
    private static async Task EnableChatRoomsCreateAsync(CustomWebApplicationFactory<Program> factory, int faceId, bool enabled)
    {
        using var admin = factory.CreateFaceClient("admin");
        var adminToken = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res = await admin.PutAsJsonAsync($"/api/faces/{faceId}", new { chatRoomsCreate = enabled });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ShouldReturn401_WithoutToken()
    {
        using var client = _factory.CreateClient();
        var faceId = 1;
        var res = await client.GetAsync($"/api/faces/{faceId}/chat-rooms");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShouldReturn404_WhenFaceDoesNotExist()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync("/api/faces/999999/chat-rooms");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ShouldReturn200_AndMarkHostFlags_ForDefaultHostUser()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync($"/api/faces/{faceId}/chat-rooms");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await res.Content.ReadFromJsonAsync<JsonElement>();
        IntegrationTestPaginatedList.ReadItems(envelope);
    }

    [Fact]
    public async Task CreateUserRoom_Should403_WhenChatRoomsCreateDisabled()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new
        {
            title = "Nope",
            isPublic = true,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUserRoom_Should403_WhenUserIsFaceHost()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new
        {
            title = "Host room",
            isPublic = true,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUserRoom_Should400_WhenTitleWhitespace()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new
        {
            title = "   ",
            isPublic = true,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUserRoom_Should201_AndCreatorIsMember()
    {
        using var client = _factory.CreateClient();
        var (token, userId, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new
        {
            title = $"Room {Guid.NewGuid():N}",
            description = "d",
            isPublic = true,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = body.GetProperty("id").GetInt32();

        var get = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/chat-rooms/{roomId}");
        get.GetProperty("isMember").GetBoolean().Should().BeTrue();
        get.GetProperty("creatorUserId").GetString().Should().Be(userId);
    }

    [Fact]
    public async Task Get_Should404_WhenRoomOnDifferentFace()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "X", isPublic = true });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (otherFaceId, _) = await IntegrationTestFaceHelper.CreateUniqueFaceAsync(_factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync($"/api/faces/{otherFaceId}/chat-rooms/{roomId}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinPublic_Should403_ForHost()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var (adminToken, adminUserId, adminEmail) = await RegisterAndLoginAsync(client);
        adminToken = await PromoteUserToGlobalAdminAsync(_factory, client, adminUserId, adminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var sys = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = $"Sys {Guid.NewGuid():N}" });
        sys.StatusCode.Should().Be(HttpStatusCode.Created);
        var roomId = (await sys.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var join = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join", null);
        join.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JoinPublic_Should200_WhenFaceUserNotMember()
    {
        using var client = _factory.CreateClient();
        var (aToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, aToken);
        var (adminToken, adminUserId, adminEmail) = await RegisterAndLoginAsync(client);
        adminToken = await PromoteUserToGlobalAdminAsync(_factory, client, adminUserId, adminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var sys = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = $"Sys {Guid.NewGuid():N}" });
        var roomId = (await sys.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (bToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, bToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bToken);
        var join = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join", null);
        join.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JoinPublic_ShouldReturnAlreadyMember_WhenDuplicate()
    {
        using var client = _factory.CreateClient();
        var (adminToken, adminUserId, adminEmail) = await RegisterAndLoginAsync(client);
        adminToken = await PromoteUserToGlobalAdminAsync(_factory, client, adminUserId, adminEmail);
        var (userToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, userToken);
        await SetFaceUserRoleAsync(client, userToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var sys = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = $"Sys {Guid.NewGuid():N}" });
        var roomId = (await sys.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        (await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("alreadyMember").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task JoinRequest_Should400_OnPublicRoom()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Pub", isPublic = true });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var res = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinRequest_Should200_AndCreateNotification_ForPrivateRoom()
    {
        using var client = _factory.CreateClient();
        var (creatorToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, creatorToken);
        await SetFaceUserRoleAsync(client, creatorToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Priv", isPublic = false });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (joinerToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, joinerToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var req = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null);
        req.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Messages_Should403_WhenNotMemberNorHost()
    {
        using var client = _factory.CreateClient();
        var (aToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, aToken);
        await SetFaceUserRoleAsync(client, aToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "M1", isPublic = true });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (bToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, bToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bToken);
        var msg = await client.GetAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/messages");
        msg.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Messages_Should200_ForHost_WithoutMembership()
    {
        using var client = _factory.CreateClient();
        var (hostToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, hostToken);
        var (adminToken, adminUserId, adminEmail) = await RegisterAndLoginAsync(client);
        adminToken = await PromoteUserToGlobalAdminAsync(_factory, client, adminUserId, adminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var sys = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = $"Sys {Guid.NewGuid():N}" });
        var roomId = (await sys.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hostToken);
        var msg = await client.GetAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/messages");
        msg.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateSystem_Should403_ForRegularUser()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = "Hack" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSystem_Should201_ForGlobalAdmin()
    {
        using var client = _factory.CreateClient();
        var (adminToken, adminUserId, adminEmail) = await RegisterAndLoginAsync(client);
        adminToken = await PromoteUserToGlobalAdminAsync(_factory, client, adminUserId, adminEmail);
        var (userToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, userToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms/system", new { title = $"Adm {Guid.NewGuid():N}" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_Should403_WhenNotCreatorNorAdmin_ForUserRoom()
    {
        using var client = _factory.CreateClient();
        var (aToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, aToken);
        await SetFaceUserRoleAsync(client, aToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Own", isPublic = true });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (bToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, bToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bToken);
        var del = await client.DeleteAsync($"/api/faces/{faceId}/chat-rooms/{roomId}");
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Should204_WhenCreatorDeletesOwnRoom()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "DelMe", isPublic = true });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var del = await client.DeleteAsync($"/api/faces/{faceId}/chat-rooms/{roomId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync($"/api/faces/{faceId}/chat-rooms/{roomId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApproveRequest_Should200_WhenCreatorApproves()
    {
        using var client = _factory.CreateClient();
        var (creatorToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, creatorToken);
        await SetFaceUserRoleAsync(client, creatorToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Apr", isPublic = false });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (joinerToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, joinerToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var req = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null);
        var requestId = (await req.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var approve = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/requests/{requestId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var get = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/chat-rooms/{roomId}");
        get.GetProperty("isMember").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DenyRequest_Should200_WhenCreatorDenies()
    {
        using var client = _factory.CreateClient();
        var (creatorToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, creatorToken);
        await SetFaceUserRoleAsync(client, creatorToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Deny", isPublic = false });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (joinerToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, joinerToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var req = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null);
        var requestId = (await req.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var deny = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/requests/{requestId}/deny", null);
        deny.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var get = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/chat-rooms/{roomId}");
        get.GetProperty("isMember").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ApproveRequest_Should403_WhenCallerIsNotCreator()
    {
        using var client = _factory.CreateClient();
        var (creatorToken, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, creatorToken);
        await SetFaceUserRoleAsync(client, creatorToken, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "P", isPublic = false });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var (joinerToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, joinerToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        var req = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null);
        var reqBody = await req.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = reqBody.GetProperty("requestId").GetInt32();

        var (otherToken, _, _) = await RegisterAndLoginAsync(client);
        await SetFaceUserRoleAsync(client, otherToken, faceId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var approve = await client.PostAsync($"/api/faces/{faceId}/chat-rooms/requests/{requestId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Messages_ShouldRespectBeforeId_Pagination()
    {
        using var client = _factory.CreateClient();
        var (token, userId, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        await SetFaceUserRoleAsync(client, token, faceId);
        await EnableChatRoomsCreateAsync(_factory, faceId, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync($"/api/faces/{faceId}/chat-rooms", new { title = "Pag", isPublic = true });
        var roomId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var room = await db.FaceChatRooms.FindAsync(roomId);
        room.Should().NotBeNull();
        for (var i = 0; i < 3; i++)
        {
            db.FaceChatRoomMessages.Add(new FaceChatRoomMessage
            {
                FaceChatRoomId = roomId,
                SenderUserId = userId,
                Content = $"m{i}",
                SentAt = DateTime.UtcNow.AddMinutes(-i),
            });
        }

        await db.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var all = await client.GetFromJsonAsync<JsonElement[]>($"/api/faces/{faceId}/chat-rooms/{roomId}/messages?pageSize=10");
        all.Should().NotBeNull();
        all!.Length.Should().Be(3);
        var beforeId = all[1].GetProperty("id").GetInt32();
        var page = await client.GetFromJsonAsync<JsonElement[]>($"/api/faces/{faceId}/chat-rooms/{roomId}/messages?pageSize=10&beforeId={beforeId}");
        page.Should().NotBeNull();
        page!.Length.Should().Be(1);
    }
}
