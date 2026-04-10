using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeDemo.Api.Tests;

/// <summary>
/// Creates faces via admin scope (global Admin JWT). Use when tests need a fresh face row; prefer seeded
/// <c>public</c>/<c>basic</c>/<c>koncept</c> when isolation tests only need distinct IDs.
/// </summary>
public static class IntegrationTestFaceHelper
{
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
