using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BeDemo.Api.Data;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorUserChatControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _oauth;
    private readonly HttpClient _adminFace;

    public OperatorUserChatControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _oauth = AclTestClients.CreateOAuthClient(factory);
        _adminFace = AclTestClients.CreateAdminFaceClient(factory);
    }

    [Fact]
    public async Task ListConversations_Should401_WithoutJwt()
    {
        var res = await _adminFace.GetAsync("/api/operator-user-chat/conversations");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListConversations_Should403_ForPlatformAdmin()
    {
        var adminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/operator-user-chat/conversations");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var res = await _adminFace.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetHistory_Should200_AfterPlatformMessage()
    {
        var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
        var targetId = await GetIntegrationAdminUserIdAsync();
        const string content = "operator user chat history test";
        await PostPlatformMessageAsync(superToken, targetId, content);

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-user-chat/with/{targetId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
        var res = await _adminFace.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("items").EnumerateArray().Should().Contain(m =>
            m.GetProperty("content").GetString()!.Contains("operator user chat"));
    }

    [Fact]
    public async Task UserReply_ShouldAppearInOperatorUserChatHistory()
    {
        var (userToken, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
        var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
        var superId = await GetSuperAdminUserIdAsync();
        (await PostPlatformMessageAsync(superToken, userId, "seed platform thread")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<BeDemo.Api.Services.IPlatformDirectMessageService>();
            var (err, _) = await platform.SendAsync(userId, superId, "user reply to super-admin");
            err.Should().BeNull();
        }

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-user-chat/with/{userId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
        var res = await _adminFace.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("items").EnumerateArray().Should().Contain(m =>
            m.GetProperty("content").GetString() == "user reply to super-admin");

        _ = userToken;
    }

    private async Task<string> GetIntegrationAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.AsNoTracking().FirstAsync(u => u.Email == IntegrationTestSeed.Email)).Id;
    }

    private async Task<string> GetSuperAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.AsNoTracking().FirstAsync(u => u.Email == IntegrationTestSeed.SuperAdminEmail)).Id;
    }

    private Task<HttpResponseMessage> PostPlatformMessageAsync(string superToken, string targetId, string content)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/operator-users/users/{targetId}/platform-messages")
        {
            Content = JsonContent.Create(new { content }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
        return _adminFace.SendAsync(req);
    }
}
