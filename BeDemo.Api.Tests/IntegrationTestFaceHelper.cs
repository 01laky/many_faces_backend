using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BeDemo.Api.Tests;

/// <summary>
/// Creates faces via admin scope (global Admin JWT). Use when tests need a fresh face row; prefer seeded
/// <c>public</c>/<c>basic</c>/<c>koncept</c> when isolation tests only need distinct IDs.
/// </summary>
public static class IntegrationTestFaceHelper
{
    /// <summary>
    /// Face id for the current URL scope after GET /api/faces/config.
    /// On the public tenant, authenticated users see every face they may access (portal switcher), so callers must
    /// pick the row whose <c>index</c> matches the scoped client (e.g. <c>public</c> vs <c>basic</c>), not <c>cfg[0]</c>.
    /// </summary>
    public static async Task<int> GetScopedFaceIdFromConfigAsync(
        HttpClient faceScopedClient,
        string bearerToken,
        string scopedFaceIndex)
    {
        faceScopedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        var cfg = await faceScopedClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        cfg.Should().NotBeNull();
        cfg!.Should().NotBeEmpty();
        foreach (var f in cfg)
        {
            if (string.Equals(f.GetProperty("index").GetString(), scopedFaceIndex, StringComparison.OrdinalIgnoreCase))
                return f.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException(
            $"Face index '{scopedFaceIndex}' not found in faces config (count={cfg.Length}).");
    }

    public static async Task<int> CreateUniqueFaceIdAsync(CustomWebApplicationFactory<Program> factory)
    {
        var (id, _) = await CreateUniqueFaceAsync(factory);
        return id;
    }

    public static async Task<(int Id, string Index)> CreateUniqueFaceAsync(CustomWebApplicationFactory<Program> factory)
    {
        using var admin = factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var index = $"itest_{Guid.NewGuid():N}";
        using var resp = await admin.PostAsJsonAsync("/api/faces", new
        {
            index,
            title = "Integration test face",
            description = "Seeded by IntegrationTestFaceHelper",
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetInt32(), index);
    }
}
