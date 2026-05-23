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

/// <summary>BE-P1…P12 — portal profile preferences (locale, last face, grid settings).</summary>
public sealed class ProfilePreferencesEdgeTests
    : IClassFixture<CustomWebApplicationFactory<Program>>,
        IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProfilePreferencesEdgeTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(HttpClient Client, string Token)> CreateAuthedClientAsync()
    {
        var token = await IntegrationTestRegistration.RegisterAndGetAccessTokenAsync(_client, _factory);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private static async Task<int> GetPublicFaceIdAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await context.Faces.AsNoTracking()
            .Where(f => f.IsPublic)
            .Select(f => f.Id)
            .FirstOrDefaultAsync();
        id.Should().BeGreaterThan(0);
        return id;
    }

    /// <summary>BE-P1 — GET me returns null prefs for new user.</summary>
    [Fact]
    public async Task BE_P1_GetMe_ReturnsNullPrefs_ForNewUser()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("preferredUiLanguage").ValueKind.Should().Be(JsonValueKind.Null);
        me.GetProperty("lastSelectedFaceId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>BE-P2 — PUT preferredUiLanguage valid de → GET reflects.</summary>
    [Fact]
    public async Task BE_P2_PutPreferredUiLanguage_Valid_ReflectsOnGet()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var put = await client.PutAsJsonAsync("/api/profile/me", new { preferredUiLanguage = "de" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("preferredUiLanguage").GetString().Should().Be("de");
    }

    /// <summary>BE-P3 — PUT preferredUiLanguage invalid xx → 400.</summary>
    [Fact]
    public async Task BE_P3_PutPreferredUiLanguage_Invalid_Returns400()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var put = await client.PutAsJsonAsync("/api/profile/me", new { preferredUiLanguage = "xx" });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>BE-P4 — PUT lastSelectedFaceId for accessible public face → GET reflects.</summary>
    [Fact]
    public async Task BE_P4_PutLastSelectedFaceId_AccessibleFace_ReflectsOnGet()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var faceId = await GetPublicFaceIdAsync(_factory.Services);

        var put = await client.PutAsJsonAsync("/api/profile/me", new { lastSelectedFaceId = faceId });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("lastSelectedFaceId").GetInt32().Should().Be(faceId);
    }

    /// <summary>BE-P5 — PUT lastSelectedFaceId unknown face → 404.</summary>
    [Fact]
    public async Task BE_P5_PutLastSelectedFaceId_UnknownFace_Returns404()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var put = await client.PutAsJsonAsync("/api/profile/me", new { lastSelectedFaceId = 9_999_999 });
        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>BE-P6 — PUT lastSelectedFaceId inaccessible private face → 403.</summary>
    [Fact]
    public async Task BE_P6_PutLastSelectedFaceId_InaccessibleFace_Returns403()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var privateFaceId = await CreatePrivateFaceWithoutUserAccessAsync();
        var put = await client.PutAsJsonAsync("/api/profile/me", new { lastSelectedFaceId = privateFaceId });
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>BE-P7 — Clearing language → GET null.</summary>
    [Fact]
    public async Task BE_P7_ClearPreferredUiLanguage_ReturnsNull()
    {
        var (client, _) = await CreateAuthedClientAsync();
        (await client.PutAsJsonAsync("/api/profile/me", new { preferredUiLanguage = "sk" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/profile/me", new { clearPreferredUiLanguage = true }))
            .EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("preferredUiLanguage").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>BE-P8 — Grid settings PUT merge → GET merged.</summary>
    [Fact]
    public async Task BE_P8_GridSettings_Merge_Persists()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var faceId = await GetPublicFaceIdAsync(_factory.Services);

        var put = await client.PutAsJsonAsync(
            $"/api/profile/me/faces/{faceId}/settings",
            new { gridComponents = new Dictionary<string, object> { ["c1"] = new { autoplay = true } } });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetFromJsonAsync<JsonElement>($"/api/profile/me/faces/{faceId}/settings");
        get.GetProperty("gridComponents").GetProperty("c1").GetProperty("autoplay").GetBoolean().Should().BeTrue();
    }

    /// <summary>BE-P9 — Grid settings over size cap → 400.</summary>
    [Fact]
    public async Task BE_P9_GridSettings_Oversized_Returns400()
    {
        var (client, _) = await CreateAuthedClientAsync();
        var faceId = await GetPublicFaceIdAsync(_factory.Services);

        var huge = new Dictionary<string, object>();
        for (var i = 0; i < 500; i++)
            huge[$"component-{i}"] = new { autoplay = true, note = new string('x', 64) };

        var put = await client.PutAsJsonAsync(
            $"/api/profile/me/faces/{faceId}/settings",
            new { gridComponents = huge });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>BE-P10 — Unauthenticated GET/PUT → 401.</summary>
    [Fact]
    public async Task BE_P10_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/profile/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PutAsJsonAsync("/api/profile/me", new { preferredUiLanguage = "en" })).StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>BE-P11 — Delete face → user LastSelectedFaceId null.</summary>
    [Fact]
    public async Task BE_P11_DeleteFace_ClearsLastSelectedFaceId()
    {
        var (client, token) = await CreateAuthedClientAsync();
        var (faceId, _) = await IntegrationTestFaceHelper.CreateUniqueFaceAsync(_factory);

        (await client.PutAsJsonAsync("/api/profile/me", new { lastSelectedFaceId = faceId }))
            .EnsureSuccessStatusCode();

        using (var admin = _factory.CreateFaceClient("admin"))
        {
            admin.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin));
            (await admin.DeleteAsync($"/api/faces/{faceId}")).EnsureSuccessStatusCode();
        }

        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("lastSelectedFaceId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>BE-P12 — Partial PUT updates gradient without clearing language.</summary>
    [Fact]
    public async Task BE_P12_PartialPut_PreservesOtherPrefs()
    {
        var (client, _) = await CreateAuthedClientAsync();
        (await client.PutAsJsonAsync("/api/profile/me", new { preferredUiLanguage = "it", enableAnimatedGradient = false }))
            .EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync("/api/profile/me", new { enableAnimatedGradient = true }))
            .EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        me.GetProperty("preferredUiLanguage").GetString().Should().Be("it");
        me.GetProperty("enableAnimatedGradient").GetBoolean().Should().BeTrue();
    }

    private async Task<int> CreatePrivateFaceWithoutUserAccessAsync()
    {
        using var admin = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var index = $"priv_{Guid.NewGuid():N}";
        using var resp = await admin.PostAsJsonAsync("/api/faces", new
        {
            index,
            title = "Private IT face",
            description = "No user access",
            isPublic = false,
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }
}
