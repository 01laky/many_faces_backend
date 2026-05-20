using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Face profile operator detail: extended GET, paginated reads, UGC delete (ADPM-B*).</summary>
public sealed class AdminProfileManagementTests
    : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private const string Password = "Test1234!@##";
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminProfileManagementTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private static object DeleteBody(int faceId, string suffix = "") => new
    {
        faceId,
        reason = $"Audit reason long enough {suffix}",
        userMessage = $"Author message long enough {suffix}",
    };

    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        using var oauth = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(oauth);
        var client = _factory.CreateFaceClient("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string roleNameSubstring)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        foreach (var r in roles!)
        {
            var name = r.GetProperty("name").GetString() ?? "";
            if (name.Contains(roleNameSubstring, StringComparison.OrdinalIgnoreCase))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {roleNameSubstring} not found");
    }

    private async Task<(int FaceId, string ProfileUserId, int CommentId, string AuthorUserId)> SeedProfileCommentAsync()
    {
        var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"adp_owner_{Guid.NewGuid():N}@test.com",
            Password,
            "Owner",
            "Profile");
        var (tokenB, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"adp_author_{Guid.NewGuid():N}@test.com",
            Password,
            "Comment",
            "Author");

        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, tokenA, "public");
        var userRoleId = await GetFaceRoleIdAsync(_client, tokenA, "FACE_USER");

        foreach (var token in new[] { tokenA, tokenB })
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await _client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId }))
                .EnsureSuccessStatusCode();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var comment = await _client.PostAsJsonAsync(
            $"/api/faces/{faceId}/profiles/{userA}/comments",
            new { body = "Operator delete target" });
        comment.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await comment.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetInt32();

        return (faceId, userA, commentId, userB);
    }

    [Fact]
    public async Task GetProfileDetail_ShouldReturnCountsAndFaceVisibility_ADPM_B1_B17()
    {
        var (faceId, profileUserId, _, _) = await SeedProfileCommentAsync();
        using var super = await CreateSuperAdminClientAsync();
        var detail = await super.GetFromJsonAsync<JsonElement>(
            $"/api/faces/{faceId}/profiles/{profileUserId}");
        detail.GetProperty("commentsCount").GetInt32().Should().BeGreaterThan(0);
        detail.TryGetProperty("faceRoleName", out _).Should().BeTrue();
        detail.TryGetProperty("faceVisibility", out _).Should().BeTrue();
        detail.GetProperty("likesCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Comments_PageEnvelope_ShouldReturnPaginatedItems_ADPM_B2_B14()
    {
        var (faceId, profileUserId, _, _) = await SeedProfileCommentAsync();
        using var super = await CreateSuperAdminClientAsync();
        var page = await super.GetFromJsonAsync<JsonElement>(
            $"/api/faces/{faceId}/profiles/{profileUserId}/comments?page=1&pageSize=10&sortBy=createdAt&sortDir=desc");
        page.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
        var first = page.GetProperty("items").EnumerateArray().First();
        first.TryGetProperty("authorDisplayName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteComment_ShouldRemoveRow_ADPM_B4()
    {
        var (faceId, profileUserId, commentId, _) = await SeedProfileCommentAsync();
        using var super = await CreateSuperAdminClientAsync();
        var del = await super.PostAsJsonAsync(
            $"/api/operator-content/profile-comments/{commentId}/delete",
            DeleteBody(faceId, "c"));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserFaceProfileComments.AnyAsync(c => c.Id == commentId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteComment_Should403_ForNonSuperAdmin_ADPM_B5()
    {
        var (faceId, _, commentId, _) = await SeedProfileCommentAsync();
        var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"adp_norm_{Guid.NewGuid():N}@test.com",
            Password,
            "Norm",
            "User");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.PostAsJsonAsync(
            $"/api/operator-content/profile-comments/{commentId}/delete",
            DeleteBody(faceId));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteComment_ShouldBeIdempotent_ADPM_B12()
    {
        var (faceId, _, commentId, _) = await SeedProfileCommentAsync();
        using var super = await CreateSuperAdminClientAsync();
        (await super.PostAsJsonAsync(
            $"/api/operator-content/profile-comments/{commentId}/delete",
            DeleteBody(faceId))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await super.PostAsJsonAsync(
            $"/api/operator-content/profile-comments/{commentId}/delete",
            DeleteBody(faceId, "2"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteComment_WrongFaceId_Should404_ADPM_B11()
    {
        var (faceId, _, commentId, _) = await SeedProfileCommentAsync();
        using var super = await CreateSuperAdminClientAsync();
        var del = await super.PostAsJsonAsync(
            $"/api/operator-content/profile-comments/{commentId}/delete",
            DeleteBody(faceId + 99999));
        del.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
