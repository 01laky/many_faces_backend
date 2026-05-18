/*
 * AlbumsControllerTests.cs - Unit tests for AlbumsController, AlbumCommentsController, AlbumLikesController
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class AlbumsControllerTests : IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
    private readonly RegistrationInviteWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public AlbumsControllerTests(RegistrationInviteWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateFaceClient("public");
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null)
            return _authToken;

        var email = $"album_test_{Guid.NewGuid()}@test.com";
        var password = "Test1234!@##";

        var tokens = await IntegrationTestRegistration.CompleteRegistrationAsync(
            _client,
            _factory,
            email,
            password,
            "Album",
            "Tester");
        _authToken = tokens.AccessToken;
        return _authToken;
    }

    private void SetAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task ApproveAsSuperAdminAsync(ModeratedContentType contentType, int contentId)
    {
        using var admin = _factory.CreateFaceClient("admin");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin));
        var response = await admin.PostAsJsonAsync(
            $"/api/contentmoderation/{contentType}/{contentId}/approve",
            new { reason = "Approved for test" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> CreateTestAlbumAsync(
        AlbumTypeEnum albumType = AlbumTypeEnum.Public,
        MediaTypeEnum mediaType = MediaTypeEnum.Image,
        List<int>? faceIds = null)
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = $"Test Album {Guid.NewGuid()}",
            description = "Test description",
            albumType = (int)albumType,
            mediaType = (int)mediaType,
            faceIds
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var album = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return album.GetProperty("id").GetInt32();
    }

    // ==================== Albums CRUD ====================

    [Fact]
    public async Task GetAlbums_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/albums");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlbums_ShouldReturnList_WhenAuthenticated()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/albums");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var albums = await response.Content.ReadFromJsonAsync<JsonElement>();
        albums.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetAlbums_ShouldIsolateAlbums_ByTenantUrlScope()
    {
        var token = await GetAuthTokenAsync();
        using var publicClient = _factory.CreateFaceClient("public");
        using var basicClient = _factory.CreateFaceClient("basic");
        publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        basicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var pubResp = await publicClient.PostAsJsonAsync("/api/albums", new
        {
            title = "Only on Public",
            albumType = 1,
            mediaType = 1,
        });
        pubResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var pubAlbum = await pubResp.Content.ReadFromJsonAsync<JsonElement>();
        await ApproveAsSuperAdminAsync(ModeratedContentType.Album, pubAlbum.GetProperty("id").GetInt32());

        var basicResp = await basicClient.PostAsJsonAsync("/api/albums", new
        {
            title = "Only on Basic",
            albumType = 1,
            mediaType = 1,
        });
        basicResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var basicAlbum = await basicResp.Content.ReadFromJsonAsync<JsonElement>();
        await ApproveAsSuperAdminAsync(ModeratedContentType.Album, basicAlbum.GetProperty("id").GetInt32());

        var publicList = await publicClient.GetFromJsonAsync<JsonElement[]>("/api/albums");
        publicList.Should().NotBeNull();
        publicList!.Select(e => e.GetProperty("title").GetString()).Should().Contain("Only on Public");
        publicList.Select(e => e.GetProperty("title").GetString()).Should().NotContain("Only on Basic");

        var basicList = await basicClient.GetFromJsonAsync<JsonElement[]>("/api/albums");
        basicList.Should().NotBeNull();
        basicList!.Select(e => e.GetProperty("title").GetString()).Should().Contain("Only on Basic");
        basicList.Select(e => e.GetProperty("title").GetString()).Should().NotContain("Only on Public");
    }

    [Fact]
    public async Task CreateAlbum_ShouldReturnCreated_WithValidData()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "My Public Album",
            description = "A nice album",
            albumType = 1,
            mediaType = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var album = await response.Content.ReadFromJsonAsync<JsonElement>();
        album.GetProperty("title").GetString().Should().Be("My Public Album");
        album.GetProperty("albumType").GetInt32().Should().Be(1);
        album.GetProperty("mediaType").GetInt32().Should().Be(1);
        album.GetProperty("approvalStatus").GetString().Should().Be(nameof(ContentApprovalStatus.PendingApproval));
        album.GetProperty("aiReviewStatus").GetString().Should().Be(nameof(AiReviewStatus.Queued));
    }

    [Fact]
    public async Task CreateAlbum_ShouldNotAppearInPublicList_UntilApproved()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "Pending Album",
            albumType = 1,
            mediaType = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var albumId = created.GetProperty("id").GetInt32();

        var list = await _client.GetFromJsonAsync<JsonElement[]>("/api/albums");
        list!.Select(e => e.GetProperty("id").GetInt32()).Should().NotContain(albumId);

        await ApproveAsSuperAdminAsync(ModeratedContentType.Album, albumId);

        var approvedList = await _client.GetFromJsonAsync<JsonElement[]>("/api/albums");
        approvedList!.Select(e => e.GetProperty("id").GetInt32()).Should().Contain(albumId);
    }

    [Fact]
    public async Task CreateAlbum_ShouldReturnBadRequest_WhenTitleEmpty()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "   ",
            albumType = 1,
            mediaType = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAlbum_ShouldAssociateFaces()
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);
        var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, token, "public");

        var response = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "Album With Faces",
            albumType = 1,
            mediaType = 1,
            faceIds = new[] { faceId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var album = await response.Content.ReadFromJsonAsync<JsonElement>();
        var albumId = album.GetProperty("id").GetInt32();

        // Verify faces are associated
        var detailResp = await _client.GetAsync($"/api/albums/{albumId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("faces").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetAlbum_ShouldReturnAlbum_WhenPublic()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.GetAsync($"/api/albums/{albumId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var album = await response.Content.ReadFromJsonAsync<JsonElement>();
        album.GetProperty("id").GetInt32().Should().Be(albumId);
    }

    [Fact]
    public async Task GetAlbum_ShouldReturnNotFound_WhenInvalidId()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/albums/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAlbum_ShouldReturnOk_WhenCreator()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.PutAsJsonAsync($"/api/albums/{albumId}", new
        {
            title = "Updated Title"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var album = await response.Content.ReadFromJsonAsync<JsonElement>();
        album.GetProperty("title").GetString().Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateAlbum_ShouldReturnBadRequest_WhenFaceIdsTargetAnotherTenant()
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);
        var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_client, token, "public");

        using var basicClient = _factory.CreateFaceClient("basic");
        var basicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(basicClient, token, "basic");

        var albumId = await CreateTestAlbumAsync(faceIds: new List<int> { publicFaceId });

        var response = await _client.PutAsJsonAsync($"/api/albums/{albumId}", new
        {
            faceIds = new[] { basicFaceId },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAlbum_ShouldReturnNoContent_WhenCreator()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.DeleteAsync($"/api/albums/{albumId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"/api/albums/{albumId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAlbum_ShouldReturnNotFound_WhenInvalidId()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.DeleteAsync("/api/albums/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlbumsByUser_ShouldReturnAlbums()
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);
        var albumId = await CreateTestAlbumAsync();

        // Get album detail to extract creatorId
        var albumResp = await _client.GetAsync($"/api/albums/{albumId}");
        albumResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var albumDetail = await albumResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = albumDetail.GetProperty("creatorId").GetString();

        var response = await _client.GetAsync($"/api/albums/user/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var albums = await response.Content.ReadFromJsonAsync<JsonElement>();
        albums.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAlbum_PrivateAndPaid_ShouldWork()
    {
        SetAuth(await GetAuthTokenAsync());

        var resp1 = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "Private Album",
            albumType = 2,
            mediaType = 1
        });
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp2 = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "Paid Album",
            albumType = 3,
            mediaType = 2
        });
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);
        var paid = await resp2.Content.ReadFromJsonAsync<JsonElement>();
        paid.GetProperty("albumType").GetInt32().Should().Be(3);
        paid.GetProperty("mediaType").GetInt32().Should().Be(2);
    }

    // ==================== Album Comments ====================

    [Fact]
    public async Task GetComments_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/albums/1/comments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateComment_ShouldReturnCreated()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new
        {
            content = "Great album!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<JsonElement>();
        comment.GetProperty("content").GetString().Should().Be("Great album!");
    }

    [Fact]
    public async Task CreateComment_ShouldReturnBadRequest_WhenContentEmpty()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new
        {
            content = "   "
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateComment_ShouldReturnNotFound_WhenAlbumMissing()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.PostAsJsonAsync("/api/albums/99999/comments", new
        {
            content = "Hello"
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetComments_ShouldReturnList()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new
        {
            content = "Comment 1"
        });

        var response = await _client.GetAsync($"/api/albums/{albumId}/comments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var comments = await response.Content.ReadFromJsonAsync<JsonElement>();
        comments.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateComment_ShouldReturnOk_WhenAuthor()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var createResp = await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new
        {
            content = "Original comment"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetInt32();

        var response = await _client.PutAsJsonAsync($"/api/albums/{albumId}/comments/{commentId}", new
        {
            content = "Updated comment"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("content").GetString().Should().Be("Updated comment");
    }

    [Fact]
    public async Task DeleteComment_ShouldReturnNoContent_WhenAuthor()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var createResp = await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new
        {
            content = "To be deleted"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetInt32();

        var response = await _client.DeleteAsync($"/api/albums/{albumId}/comments/{commentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ==================== Album Likes ====================

    [Fact]
    public async Task LikeAlbum_ShouldReturnOk()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.PostAsync($"/api/albums/{albumId}/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LikeAlbum_ShouldReturnBadRequest_WhenAlreadyLiked()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        await _client.PostAsync($"/api/albums/{albumId}/likes", null);
        var response = await _client.PostAsync($"/api/albums/{albumId}/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LikeAlbum_ShouldReturnNotFound_WhenAlbumMissing()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.PostAsync("/api/albums/99999/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlikeAlbum_ShouldReturnOk()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        await _client.PostAsync($"/api/albums/{albumId}/likes", null);
        var response = await _client.DeleteAsync($"/api/albums/{albumId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlikeAlbum_ShouldReturnNotFound_WhenNotLiked()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        var response = await _client.DeleteAsync($"/api/albums/{albumId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLikes_ShouldReturnList()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        await _client.PostAsync($"/api/albums/{albumId}/likes", null);

        var response = await _client.GetAsync($"/api/albums/{albumId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var likes = await response.Content.ReadFromJsonAsync<JsonElement>();
        likes.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetAlbumDetail_ShouldIncludeLikeAndCommentCounts()
    {
        SetAuth(await GetAuthTokenAsync());
        var albumId = await CreateTestAlbumAsync();

        await _client.PostAsync($"/api/albums/{albumId}/likes", null);
        await _client.PostAsJsonAsync($"/api/albums/{albumId}/comments", new { content = "Nice!" });

        var response = await _client.GetAsync($"/api/albums/{albumId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var album = await response.Content.ReadFromJsonAsync<JsonElement>();
        album.GetProperty("likesCount").GetInt32().Should().Be(1);
        album.GetProperty("commentsCount").GetInt32().Should().Be(1);
        album.GetProperty("isLikedByMe").GetBoolean().Should().BeTrue();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
