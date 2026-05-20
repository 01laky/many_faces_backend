using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Face chat room operator detail: extended GET, inventory reads, hard-delete (CDRM-B*).</summary>
public sealed class AdminChatRoomManagementTests
    : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private const string Password = "Test1234!@##";
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminChatRoomManagementTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private static object DeleteBody(int faceId, string suffix = "") => new
    {
        faceId,
        reason = $"Audit reason long enough {suffix}",
        userMessage = $"Creator message long enough {suffix}",
    };

    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        using var oauth = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(oauth);
        var client = _factory.CreateFaceClient("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(int RoomId, int FaceId, string CreatorId)> SeedUserRoomAsync(
        bool isPublic = true,
        bool withMessages = false)
    {
        var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"crm_{Guid.NewGuid():N}@test.com",
            Password,
            "Chat",
            "Room");

        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, token, "public");
        await EnableChatRoomsCreateAsync(faceId, true);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var roleId = await GetFaceRoleIdAsync(token, "FACE_USER");
        var roleRes = await _client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId });
        roleRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await _client.PostAsJsonAsync(
            $"/api/faces/{faceId}/chat-rooms",
            new { title = $"Room {Guid.NewGuid():N}", isPublic, description = "Demo description" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = created.GetProperty("id").GetInt32();

        if (withMessages)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.FaceChatRoomMessages.Add(new FaceChatRoomMessage
            {
                FaceChatRoomId = roomId,
                SenderUserId = userId,
                Content = "Hello operator",
                SentAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return (roomId, faceId, userId);
    }

    private async Task EnableChatRoomsCreateAsync(int faceId, bool enabled)
    {
        using var admin = _factory.CreateFaceClient("admin");
        var adminToken = await IntegrationTestSeed.GetAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res = await admin.PutAsJsonAsync($"/api/faces/{faceId}", new { chatRoomsCreate = enabled });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> GetFaceRoleIdAsync(string token, string exactName)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await _client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        foreach (var r in roles!)
        {
            if (string.Equals(r.GetProperty("name").GetString(), exactName, StringComparison.Ordinal))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {exactName} not found");
    }

    [Fact]
    public async Task GetRoomDetail_ShouldReturnCountsAndTimestamps_CDRM_B1()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync(withMessages: true);
        using var super = await CreateSuperAdminClientAsync();
        var detail = await super.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/chat-rooms/{roomId}");
        detail.GetProperty("messageCount").GetInt32().Should().BeGreaterThan(0);
        detail.TryGetProperty("updatedAt", out _).Should().BeTrue();
        detail.TryGetProperty("pendingJoinRequestCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Messages_Should200_ForSuperAdmin_WithoutMembership_CDRM_B2()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync(withMessages: true);
        using var super = await CreateSuperAdminClientAsync();
        var msg = await super.GetAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/messages?pageSize=10");
        msg.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await msg.Content.ReadFromJsonAsync<JsonElement[]>();
        items.Should().NotBeNullOrEmpty();
        items![0].TryGetProperty("senderDisplayName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Messages_PageEnvelope_ShouldReturnPaginatedItems_ForOperator()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync(withMessages: true);
        using var super = await CreateSuperAdminClientAsync();
        var page = await super.GetFromJsonAsync<JsonElement>(
            $"/api/faces/{faceId}/chat-rooms/{roomId}/messages?page=1&pageSize=10&sortBy=sentAt&sortDir=desc");
        page.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        page.GetProperty("totalPages").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HardDeleteRoom_ShouldRemoveRow_AndSendDm_CDRM_B4_B8()
    {
        var (roomId, faceId, creatorId) = await SeedUserRoomAsync();
        using var super = await CreateSuperAdminClientAsync();

        var del = await super.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId, "1"));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.FaceChatRooms.AnyAsync(r => r.Id == roomId)).Should().BeFalse();
        var dm = await db.Messages
            .Where(m => m.IsPlatformDirectMessage && m.ReceiverId == creatorId)
            .OrderByDescending(m => m.Id)
            .FirstAsync();
        dm.Content.Should().Contain("Creator message long enough 1");
    }

    [Fact]
    public async Task HardDeleteRoom_Should403_ForNonSuperAdmin_CDRM_B5()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync();
        var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"crm_norm_{Guid.NewGuid():N}@test.com",
            Password,
            "Norm",
            "User");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HardDeleteRoom_WrongFaceId_ShouldNotDeleteRoom_CDRM_B6()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync();
        using var super = await CreateSuperAdminClientAsync();
        var del = await super.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId + 99999));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.FaceChatRooms.AnyAsync(r => r.Id == roomId)).Should().BeTrue();
    }

    [Fact]
    public async Task HardDeleteRoom_ShouldBeIdempotent_CDRM_B7()
    {
        var (roomId, faceId, _) = await SeedUserRoomAsync();
        using var super = await CreateSuperAdminClientAsync();
        (await super.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await super.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId, "2"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMembers_ShouldReturnPaginatedEnvelope_CDRM_B10()
    {
        var (roomId, faceId, creatorId) = await SeedUserRoomAsync();
        using var super = await CreateSuperAdminClientAsync();
        var members = await super.GetFromJsonAsync<JsonElement>(
            $"/api/faces/{faceId}/chat-rooms/{roomId}/members?page=1&pageSize=20");
        members.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
        members.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("userId").GetString() == creatorId);
    }

    [Fact]
    public async Task ListJoinRequests_ShouldReturnPendingOnly_CDRM_B11_B15()
    {
        var (roomId, faceId, creatorId) = await SeedUserRoomAsync(isPublic: false);
        var (joinerToken, joinerId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"crm_join_{Guid.NewGuid():N}@test.com",
            Password,
            "Join",
            "Er");
        await SetFaceUserRoleForUserAsync(joinerToken, faceId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinerToken);
        (await _client.PostAsync($"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var super = await CreateSuperAdminClientAsync();
        var detail = await super.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/chat-rooms/{roomId}");
        detail.GetProperty("pendingJoinRequestCount").GetInt32().Should().Be(1);

        var pending = await super.GetFromJsonAsync<JsonElement>(
            $"/api/faces/{faceId}/chat-rooms/{roomId}/join-requests?page=1&pageSize=20");
        var items = pending.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("userId").GetString().Should().Be(joinerId);
        items[0].GetProperty("status").GetString().Should().Be(nameof(FaceChatRoomJoinRequestStatus.Pending));
        _ = creatorId;
    }

    [Fact]
    public async Task HardDeleteSystemRoom_Should204_WithoutPlatformDm_CDRM_B13()
    {
        using var super = await CreateSuperAdminClientAsync();
        var (userToken, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"crm_sys_{Guid.NewGuid():N}@test.com",
            Password,
            "Sys",
            "User");
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, userToken, "public");
        var sys = await super.PostAsJsonAsync(
            $"/api/faces/{faceId}/chat-rooms/system",
            new { title = $"Sys {Guid.NewGuid():N}" });
        sys.StatusCode.Should().Be(HttpStatusCode.Created);
        var roomId = (await sys.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var beforeDms = await CountAllPlatformDmsAsync();
        var del = await super.PostAsJsonAsync(
            $"/api/operator-content/chat-rooms/{roomId}/delete",
            DeleteBody(faceId));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CountAllPlatformDmsAsync()).Should().Be(beforeDms);
    }

    private async Task SetFaceUserRoleForUserAsync(string token, int faceId)
    {
        var roleId = await GetFaceRoleIdAsync(token, "FACE_USER");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> CountAllPlatformDmsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Messages.CountAsync(m => m.IsPlatformDirectMessage);
    }
}
