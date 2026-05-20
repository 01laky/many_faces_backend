using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class FaceProfilesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public FaceProfilesControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    private async Task<(string Token, string UserId)> RegisterAndLoginAsync(HttpClient client)
    {
        var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            client,
            _factory,
            $"fp_{Guid.NewGuid()}@test.com",
            "Test1234!@##",
            "Fp",
            "Test");
        return (token, userId);
    }

    private static async Task<int> GetAnyFaceIdAsync(HttpClient client, string token) =>
        await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string roleNameSubstring)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        roles.Should().NotBeNull();
        foreach (var r in roles!)
        {
            var name = r.GetProperty("name").GetString() ?? "";
            if (name.Contains(roleNameSubstring, StringComparison.OrdinalIgnoreCase))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {roleNameSubstring} not found");
    }

    [Fact]
    public async Task MarkVisit_ShouldSetVisited_ThenConfigReflects()
    {
        using var client = _factory.CreateClient();
        var (token, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var visit = await client.PostAsync($"/api/faces/{faceId}/visit", null);
        visit.StatusCode.Should().Be(HttpStatusCode.OK);

        var cfg = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        var face = cfg!.First(f => f.GetProperty("id").GetInt32() == faceId);
        face.GetProperty("myVisited").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ProfileList_ShouldIncludeUser_AfterNonHostRole()
    {
        using var client = _factory.CreateClient();
        var (token, userId) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var userRoleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var listBefore = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/profiles?pageSize=100");
        var itemsBefore = listBefore!.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("userId").GetString()).ToHashSet();
        itemsBefore.Should().NotContain(userId);

        var put = await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var listAfter = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/profiles?pageSize=100");
        var itemsAfter = listAfter!.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("userId").GetString()).ToHashSet();
        itemsAfter.Should().Contain(userId);
    }

    [Fact]
    public async Task ExitFace_ShouldResetToHost_AndRemoveFromList()
    {
        using var client = _factory.CreateClient();
        var (token, userId) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var userRoleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();

        var exit = await client.PostAsync($"/api/faces/{faceId}/exit-face", null);
        exit.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/profiles?pageSize=100");
        var ids = list!.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("userId").GetString()).ToHashSet();
        ids.Should().NotContain(userId);
    }

    [Fact]
    public async Task Reviews_ShouldBeEmpty_WhenFaceDisallowsRecensions()
    {
        using var client = _factory.CreateClient();
        var (token, userId) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var userRoleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();

        var rev = await client.GetAsync($"/api/faces/{faceId}/profiles/{userId}/reviews");
        rev.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = await rev.Content.ReadFromJsonAsync<JsonElement[]>();
        arr.Should().NotBeNull();
        arr!.Length.Should().Be(0);
    }

    [Fact]
    public async Task LikeAndComment_ShouldWork_OnPublicVisibility()
    {
        using var client = _factory.CreateClient();
        var (tokenA, userA) = await RegisterAndLoginAsync(client);
        var (tokenB, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, tokenA);
        var userRoleId = await GetFaceRoleIdAsync(client, tokenA, "FACE_USER");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();

        var like = await client.PostAsync($"/api/faces/{faceId}/profiles/{userA}/like", null);
        like.StatusCode.Should().Be(HttpStatusCode.OK);

        var comment = await client.PostAsJsonAsync($"/api/faces/{faceId}/profiles/{userA}/comments", new { body = "Hello" });
        comment.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}/profiles/{userA}");
        detail.GetProperty("likedByMe").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Comments_WithoutPage_ShouldReturnLegacyArray_ADPM_B3()
    {
        using var client = _factory.CreateClient();
        var (tokenA, userA) = await RegisterAndLoginAsync(client);
        var (tokenB, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, tokenA);
        var userRoleId = await GetFaceRoleIdAsync(client, tokenA, "FACE_USER");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/profiles/{userA}/comments",
            new { body = "Portal array" })).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB);
        var res = await client.GetAsync($"/api/faces/{faceId}/profiles/{userA}/comments");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = await res.Content.ReadFromJsonAsync<JsonElement[]>();
        arr.Should().NotBeNull();
        arr!.Length.Should().BeGreaterThan(0);
    }
}
