using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Story operator detail: hard-delete, image delete, list creatorId, detail faces (SDM prompt §10).</summary>
public sealed class AdminStoryManagementTests
    : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _adminClient;
    private string? _token;

    public AdminStoryManagementTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _adminClient = factory.CreateFaceClient("admin");
    }

    public void Dispose() => _adminClient.Dispose();

    private async Task AuthorizeAdminAsync()
    {
        _token ??= await IntegrationTestSeed.GetAdminAccessTokenAsync(_adminClient);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        using var oauth = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(oauth);
        var client = _factory.CreateFaceClient("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object DeleteBody(int faceId, string suffix = "") => new
    {
        faceId,
        reason = $"Audit reason long enough {suffix}",
        userMessage = $"Creator message long enough {suffix}",
    };

    private static async Task UploadStoryImageAsync(HttpClient client, int storyId, int sortOrder)
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "slide.png");
        content.Add(new StringContent(sortOrder.ToString()), "sortOrder");
        var upload = await client.PostAsync($"/api/stories/{storyId}/images", content);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(int StoryId, int FaceId, string CreatorId)> SeedStoryAsync(bool live = true, int imageCount = 1)
    {
        await AuthorizeAdminAsync();
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(
            _adminClient,
            _token!,
            "public");

        var create = await _adminClient.PostAsJsonAsync(
            "/api/stories",
            new { title = $"Mgmt Story {Guid.NewGuid():N}", faceIds = new[] { faceId } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var storyId = created.GetProperty("id").GetInt32();

        for (var i = 0; i < imageCount; i++)
            await UploadStoryImageAsync(_adminClient, storyId, i);

        if (live)
        {
            var publish = await _adminClient.PostAsJsonAsync($"/api/stories/{storyId}/publish", new { });
            publish.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var detail = await _adminClient.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        var creatorId = detail.GetProperty("creatorId").GetString()!;
        return (storyId, faceId, creatorId);
    }

    [Fact]
    public async Task GetStoryDetail_operator_draft_includes_faces_array()
    {
        await AuthorizeAdminAsync();
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_adminClient, _token!, "public");
        var create = await _adminClient.PostAsJsonAsync(
            "/api/stories",
            new { title = $"Draft faces {Guid.NewGuid():N}", faceIds = new[] { faceId } });
        var storyId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        var detail = await _adminClient.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        detail.GetProperty("faces").EnumerateArray().Should().NotBeEmpty();
        detail.TryGetProperty("updatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ListStories_ByCreatorId_ShouldReturnOnlyThatCreator()
    {
        var (storyId, faceId, creatorId) = await SeedStoryAsync();
        using var super = await CreateSuperAdminClientAsync();
        var list = await super.GetFromJsonAsync<JsonElement>($"/api/stories?creatorId={creatorId}&page=1&pageSize=50");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(i => i.GetProperty("id").GetInt32() == storyId);
        items.Should().OnlyContain(i => i.GetProperty("creatorId").GetString() == creatorId);
        _ = faceId;
    }

    [Fact]
    public async Task ListStories_isPublishedFilter_ShouldReturnOnlyMatchingDraftOrLive()
    {
        await AuthorizeAdminAsync();
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_adminClient, _token!, "public");

        var draftCreate = await _adminClient.PostAsJsonAsync(
            "/api/stories",
            new { title = $"Filter draft {Guid.NewGuid():N}", faceIds = new[] { faceId } });
        draftCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var draftId = (await draftCreate.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        var (publishedId, _, _) = await SeedStoryAsync(live: true, imageCount: 1);

        var drafts = await _adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/stories?faceId={faceId}&isPublished=false&page=1&pageSize=100");
        var draftIds = drafts.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        draftIds.Should().Contain(draftId);
        draftIds.Should().NotContain(publishedId);

        var published = await _adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/stories?faceId={faceId}&isPublished=true&page=1&pageSize=100");
        var publishedIds = published.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        publishedIds.Should().Contain(publishedId);
        publishedIds.Should().NotContain(draftId);
    }

    [Fact]
    public async Task StoryListQuery_WithoutFaceOrCreator_ShouldReturn400()
    {
        using var super = await CreateSuperAdminClientAsync();
        var res = await super.GetAsync("/api/stories?page=1&pageSize=10");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HardDeleteStory_ShouldRemoveRow_AndSendDm()
    {
        var (storyId, faceId, creatorId) = await SeedStoryAsync();
        using var super = await CreateSuperAdminClientAsync();

        var first = await super.PostAsJsonAsync(
            $"/api/operator-content/stories/{storyId}/delete",
            DeleteBody(faceId, "first"));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var exists = await super.GetAsync($"/api/stories/{storyId}?faceId={faceId}");
        exists.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Stories.AnyAsync(s => s.Id == storyId)).Should().BeFalse();
            var dm = await db.Messages
                .Where(m => m.IsPlatformDirectMessage && m.ReceiverId == creatorId)
                .OrderByDescending(m => m.Id)
                .FirstAsync();
            dm.Content.Should().Contain("Creator message long enough first");
        }
    }

    [Fact]
    public async Task HardDeleteStory_WrongFace_ShouldReturn204_Idempotent()
    {
        var (storyId, faceId, _) = await SeedStoryAsync();
        using var super = await CreateSuperAdminClientAsync();
        var res = await super.PostAsJsonAsync(
            $"/api/operator-content/stories/{storyId}/delete",
            DeleteBody(faceId + 9999));
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task HardDeleteStory_Twice_ShouldReturn204()
    {
        var (storyId, faceId, _) = await SeedStoryAsync();
        using var super = await CreateSuperAdminClientAsync();
        await super.PostAsJsonAsync($"/api/operator-content/stories/{storyId}/delete", DeleteBody(faceId));
        var second = await super.PostAsJsonAsync($"/api/operator-content/stories/{storyId}/delete", DeleteBody(faceId, "2"));
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteStoryImage_OnDraft_ShouldRemoveRow()
    {
        await AuthorizeAdminAsync();
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_adminClient, _token!, "public");
        var create = await _adminClient.PostAsJsonAsync(
            "/api/stories",
            new { title = $"Img del {Guid.NewGuid():N}", faceIds = new[] { faceId } });
        var storyId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        await UploadStoryImageAsync(_adminClient, storyId, 0);
        var detail = await _adminClient.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        var imageId = detail.GetProperty("images")[0].GetProperty("id").GetInt32();

        using var super = await CreateSuperAdminClientAsync();
        var res = await super.PostAsJsonAsync(
            $"/api/operator-content/stories/{storyId}/images/{imageId}/delete",
            DeleteBody(faceId));
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await super.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        after.GetProperty("images").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteStoryImage_LastOnLiveStory_ShouldReturn400()
    {
        var (storyId, faceId, _) = await SeedStoryAsync(live: true, imageCount: 2);
        using var super = await CreateSuperAdminClientAsync();
        var detail = await super.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        var images = detail.GetProperty("images").EnumerateArray().ToList();
        images.Should().HaveCountGreaterThanOrEqualTo(2);
        foreach (var img in images.Skip(1))
        {
            var mid = await super.PostAsJsonAsync(
                $"/api/operator-content/stories/{storyId}/images/{img.GetProperty("id").GetInt32()}/delete",
                DeleteBody(faceId, "mid"));
            mid.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var lastId = images[0].GetProperty("id").GetInt32();
        var last = await super.PostAsJsonAsync(
            $"/api/operator-content/stories/{storyId}/images/{lastId}/delete",
            DeleteBody(faceId, "last"));
        last.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await last.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("image_delete_blocked_live");
    }

    [Fact]
    public async Task OperatorStoryManagementService_IsStoryLive_matches_portal_window()
    {
        var now = DateTime.UtcNow;
        var live = new Story
        {
            State = StoryState.Published,
            PublishedAt = now.AddMinutes(-5),
            ExpiresAt = now.AddHours(1),
        };
        OperatorStoryManagementService.IsStoryLive(live).Should().BeTrue();
        live.ExpiresAt = now.AddMinutes(-1);
        OperatorStoryManagementService.IsStoryLive(live).Should().BeFalse();
    }
}
