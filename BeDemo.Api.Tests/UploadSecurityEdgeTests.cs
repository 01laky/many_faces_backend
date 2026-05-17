using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// SHV2 BE-U4 / BE-U2 — upload path containment and size error messaging (integration + edge cases).
/// </summary>
public sealed class UploadSecurityEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UploadSecurityEdgeTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Avatar_upload_over_limit_returns_30_mb_message()
    {
        var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"avatar_size_{Guid.NewGuid():N}@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var multipart = new MultipartFormDataContent();
        var oversize = new byte[UploadLimits.AvatarMaxBytes + 1];
        // Minimal JPEG header so extension/content-type path is plausible before size check order — size checked before magic bytes in SaveAvatarFile.
        oversize[0] = 0xFF;
        oversize[1] = 0xD8;
        oversize[2] = 0xFF;
        var filePart = new ByteArrayContent(oversize);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(filePart, "file", "big.jpg");

        var response = await _client.PostAsync("/api/profile/me/avatar", multipart);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("30 MB");
        body.Should().Contain(UploadLimits.FormatMaxFileSizeMessage(UploadLimits.AvatarMaxBytes));
    }

    [Fact]
    public async Task Story_image_saved_under_story_directory_not_web_root_escape()
    {
        var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            _client,
            _factory,
            $"story_path_{Guid.NewGuid():N}@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/stories", new { title = "Upload path test" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var storyId = created.GetProperty("id").GetInt32();

        using var multipart = new MultipartFormDataContent();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var filePart = new ByteArrayContent(png);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(filePart, "file", "tile.png");
        multipart.Add(new StringContent("0"), "sortOrder");

        var upload = await _client.PostAsync($"/api/stories/{storyId}/images", multipart);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var imageUrl = payload.GetProperty("imageUrl").GetString();
        imageUrl.Should().Contain("/api/uploads/serve");
        imageUrl.Should().Contain($"path=%2Fuploads%2Fstories%2F{storyId}%2F");
        imageUrl.Should().NotContain("..");
    }
}
