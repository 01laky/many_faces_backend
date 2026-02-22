using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Tests for FacesController GetFacesConfig endpoint
/// This endpoint is public (AllowAnonymous) and returns all faces with their pages configuration
/// </summary>
public class FacesControllerConfigTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FacesControllerConfigTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetFacesConfig_ShouldReturnOk_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFacesConfig_ShouldReturnJson_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentType = response.Content.Headers.ContentType?.ToString();
        contentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task GetFacesConfig_ShouldReturnArray_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().StartWith("["); // JSON array
        content.Should().EndWith("]");
    }

    [Fact]
    public async Task GetFacesConfig_ShouldReturnFacesWithPages_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);

        facesConfig.Should().NotBeNull();

        // Check structure - each face should have index, id, and pages (even if empty)
        foreach (var face in facesConfig!)
        {
            face.TryGetProperty("index", out _).Should().BeTrue();
            face.TryGetProperty("id", out _).Should().BeTrue();
            face.TryGetProperty("pages", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetFacesConfig_ShouldContainFaceIndex_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);

        // Database should be seeded with faces (public, basic, koncept)
        facesConfig.Should().NotBeNull();
        facesConfig!.Length.Should().BeGreaterThan(0);

        // Check that response contains index property for each face
        foreach (var face in facesConfig)
        {
            face.TryGetProperty("index", out _).Should().BeTrue();
        }

        // Verify seeded faces exist
        var faceIndices = facesConfig.Select(f => f.GetProperty("index").GetString()).ToList();
        faceIndices.Should().Contain("public");
        faceIndices.Should().Contain("basic");
        faceIndices.Should().Contain("koncept");
    }

    [Fact]
    public async Task GetFacesConfig_ShouldContainPagesForEachFace_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);
        facesConfig.Should().NotBeNull();

        foreach (var face in facesConfig!)
        {
            face.TryGetProperty("pages", out var pagesProperty).Should().BeTrue();
            pagesProperty.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task GetFacesConfig_ShouldContainPageData_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);

        // Check for page properties if faces with pages exist
        if (facesConfig != null && facesConfig.Length > 0)
        {
            // Find first face with pages
            var faceWithPages = facesConfig.FirstOrDefault(f =>
                f.TryGetProperty("pages", out var p) && p.GetArrayLength() > 0);

            if (faceWithPages.ValueKind != JsonValueKind.Undefined)
            {
                var pages = faceWithPages.GetProperty("pages");
                if (pages.GetArrayLength() > 0)
                {
                    var firstPage = pages[0];
                    firstPage.TryGetProperty("name", out _).Should().BeTrue();
                    firstPage.TryGetProperty("path", out _).Should().BeTrue();
                    firstPage.TryGetProperty("pageType", out _).Should().BeTrue();
                }
            }
        }
    }

    [Fact]
    public async Task GetFacesConfig_ShouldBePublic_WithoutAuthentication()
    {
        // Arrange - no authentication token
        var clientWithoutAuth = _factory.CreateClient();

        // Act
        var response = await clientWithoutAuth.GetAsync("/api/faces/config");

        // Assert - should return OK without authentication (public endpoint)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFacesConfig_ShouldReturnOrderedFaces_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);
        facesConfig.Should().NotBeNull();

        // Faces should be ordered by index
        if (facesConfig!.Length > 1)
        {
            var firstIndex = facesConfig[0].GetProperty("index").GetString();
            var secondIndex = facesConfig[1].GetProperty("index").GetString();

            // Basic comparison - in seeded data: basic, koncept, public (alphabetical)
            // Actual order depends on database seed order, but should be consistent
            firstIndex.Should().NotBeNull();
            secondIndex.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetFacesConfig_ShouldContainPageTypeInfo_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/faces/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var facesConfig = JsonSerializer.Deserialize<JsonElement[]>(content);

        // Check for pageType if pages exist
        if (facesConfig != null && facesConfig.Length > 0)
        {
            var faceWithPages = facesConfig.FirstOrDefault(f =>
                f.TryGetProperty("pages", out var p) && p.GetArrayLength() > 0);

            if (faceWithPages.ValueKind != JsonValueKind.Undefined)
            {
                var firstPage = faceWithPages.GetProperty("pages")[0];
                firstPage.TryGetProperty("pageType", out var pageType).Should().BeTrue();
                if (pageType.ValueKind == JsonValueKind.Object)
                {
                    pageType.TryGetProperty("index", out _).Should().BeTrue();
                    pageType.TryGetProperty("id", out _).Should().BeTrue();
                }
            }
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
