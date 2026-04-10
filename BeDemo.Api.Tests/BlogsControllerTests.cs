/*
 * BlogsControllerTests.cs - Unit tests for BlogsController, BlogCommentsController, BlogLikesController
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class BlogsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public BlogsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null)
            return _authToken;

        var email = $"blog_test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Blog",
            lastName = "Tester"
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password
        };

        HttpResponseMessage? response = null;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        _authToken = tokenResponse!.AccessToken;
        return _authToken;
    }

    private void SetAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private Task<int> CreateTestFaceAsync() => IntegrationTestFaceHelper.CreateUniqueFaceIdAsync(_factory);

    private async Task<int> CreateTestBlogAsync(int? faceId = null, List<string>? imageUrls = null)
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);

        var face = faceId ?? await CreateTestFaceAsync();

        var resp = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = $"Test Blog {Guid.NewGuid()}",
            content = "<p>This is test blog content</p>",
            faceId = face,
            imageUrls
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var blog = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return blog.GetProperty("id").GetInt32();
    }

    // ==================== Blogs CRUD ====================

    [Fact]
    public async Task GetBlogs_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/blogs");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBlogs_ShouldReturnList_WhenAuthenticated()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/blogs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blogs = await response.Content.ReadFromJsonAsync<JsonElement>();
        blogs.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CreateBlog_ShouldReturnCreated_WithValidData()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "My First Blog",
            content = "<p>Hello world!</p>",
            faceId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        blog.GetProperty("title").GetString().Should().Be("My First Blog");
        blog.GetProperty("content").GetString().Should().Be("<p>Hello world!</p>");
        blog.GetProperty("faceId").GetInt32().Should().Be(faceId);
    }

    [Fact]
    public async Task CreateBlog_ShouldReturnBadRequest_WhenTitleEmpty()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "   ",
            content = "<p>Content</p>",
            faceId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBlog_ShouldReturnBadRequest_WhenContentEmpty()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "Valid Title",
            content = "  ",
            faceId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBlog_ShouldReturnBadRequest_WhenFaceIdInvalid()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "Valid Title",
            content = "<p>Content</p>",
            faceId = 99999
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBlog_ShouldCreateWithImages()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "Blog With Images",
            content = "<p>Content with images</p>",
            faceId,
            imageUrls = new[] { "https://example.com/img1.jpg", "https://example.com/img2.jpg" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        var blogId = blog.GetProperty("id").GetInt32();

        // Verify images via detail
        var detailResp = await _client.GetAsync($"/api/blogs/{blogId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("images").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task CreateBlog_ShouldLimitToThreeImages()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();

        var response = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "Blog With Too Many Images",
            content = "<p>Content</p>",
            faceId,
            imageUrls = new[] {
                "https://example.com/img1.jpg",
                "https://example.com/img2.jpg",
                "https://example.com/img3.jpg",
                "https://example.com/img4.jpg"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        var blogId = blog.GetProperty("id").GetInt32();

        var detailResp = await _client.GetAsync($"/api/blogs/{blogId}");
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("images").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GetBlog_ShouldReturnBlog_WhenExists()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.GetAsync($"/api/blogs/{blogId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        blog.GetProperty("id").GetInt32().Should().Be(blogId);
    }

    [Fact]
    public async Task GetBlog_ShouldReturnNotFound_WhenInvalidId()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/blogs/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBlogs_ShouldFilterByTenantScopedFaceId_FromMiddleware()
    {
        SetAuth(await GetAuthTokenAsync());
        var cfg = await _client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        var scopedFaceId = cfg![0].GetProperty("id").GetInt32();
        await CreateTestBlogAsync(faceId: scopedFaceId);
        await CreateTestBlogAsync(faceId: scopedFaceId);

        var response = await _client.GetAsync("/api/blogs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blogs = await response.Content.ReadFromJsonAsync<JsonElement>();
        blogs.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        foreach (var b in blogs.EnumerateArray())
            b.GetProperty("faceId").GetInt32().Should().Be(scopedFaceId);
    }

    [Fact]
    public async Task UpdateBlog_ShouldReturnOk_WhenCreator()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.PutAsJsonAsync($"/api/blogs/{blogId}", new
        {
            title = "Updated Title"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        blog.GetProperty("title").GetString().Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBlog_ShouldUpdateImages()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync(imageUrls: new List<string> { "https://example.com/old.jpg" });

        var response = await _client.PutAsJsonAsync($"/api/blogs/{blogId}", new
        {
            imageUrls = new[] { "https://example.com/new1.jpg", "https://example.com/new2.jpg" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailResp = await _client.GetAsync($"/api/blogs/{blogId}");
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("images").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task DeleteBlog_ShouldReturnNoContent_WhenCreator()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.DeleteAsync($"/api/blogs/{blogId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"/api/blogs/{blogId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBlog_ShouldReturnNotFound_WhenInvalidId()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.DeleteAsync("/api/blogs/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== Blog Comments ====================

    [Fact]
    public async Task GetBlogComments_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/blogs/1/comments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBlogComment_ShouldReturnCreated()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new
        {
            content = "Great blog post!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<JsonElement>();
        comment.GetProperty("content").GetString().Should().Be("Great blog post!");
    }

    [Fact]
    public async Task CreateBlogComment_ShouldReturnBadRequest_WhenContentEmpty()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new
        {
            content = "   "
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBlogComment_ShouldReturnNotFound_WhenBlogMissing()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.PostAsJsonAsync("/api/blogs/99999/comments", new
        {
            content = "Hello"
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBlogComments_ShouldReturnList()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new
        {
            content = "Comment 1"
        });

        var response = await _client.GetAsync($"/api/blogs/{blogId}/comments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var comments = await response.Content.ReadFromJsonAsync<JsonElement>();
        comments.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateBlogComment_ShouldReturnOk_WhenAuthor()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var createResp = await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new
        {
            content = "Original comment"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetInt32();

        var response = await _client.PutAsJsonAsync($"/api/blogs/{blogId}/comments/{commentId}", new
        {
            content = "Updated comment"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("content").GetString().Should().Be("Updated comment");
    }

    [Fact]
    public async Task DeleteBlogComment_ShouldReturnNoContent_WhenAuthor()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var createResp = await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new
        {
            content = "To be deleted"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetInt32();

        var response = await _client.DeleteAsync($"/api/blogs/{blogId}/comments/{commentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ==================== Blog Likes ====================

    [Fact]
    public async Task LikeBlog_ShouldReturnOk()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.PostAsync($"/api/blogs/{blogId}/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LikeBlog_ShouldReturnBadRequest_WhenAlreadyLiked()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        await _client.PostAsync($"/api/blogs/{blogId}/likes", null);
        var response = await _client.PostAsync($"/api/blogs/{blogId}/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LikeBlog_ShouldReturnNotFound_WhenBlogMissing()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.PostAsync("/api/blogs/99999/likes", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlikeBlog_ShouldReturnOk()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        await _client.PostAsync($"/api/blogs/{blogId}/likes", null);
        var response = await _client.DeleteAsync($"/api/blogs/{blogId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlikeBlog_ShouldReturnNotFound_WhenNotLiked()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        var response = await _client.DeleteAsync($"/api/blogs/{blogId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBlogLikes_ShouldReturnList()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        await _client.PostAsync($"/api/blogs/{blogId}/likes", null);

        var response = await _client.GetAsync($"/api/blogs/{blogId}/likes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var likes = await response.Content.ReadFromJsonAsync<JsonElement>();
        likes.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetBlogDetail_ShouldIncludeLikeAndCommentCounts()
    {
        SetAuth(await GetAuthTokenAsync());
        var blogId = await CreateTestBlogAsync();

        await _client.PostAsync($"/api/blogs/{blogId}/likes", null);
        await _client.PostAsJsonAsync($"/api/blogs/{blogId}/comments", new { content = "Nice!" });

        var response = await _client.GetAsync($"/api/blogs/{blogId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blog = await response.Content.ReadFromJsonAsync<JsonElement>();
        blog.GetProperty("likesCount").GetInt32().Should().Be(1);
        blog.GetProperty("commentsCount").GetInt32().Should().Be(1);
        blog.GetProperty("isLikedByMe").GetBoolean().Should().BeTrue();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
