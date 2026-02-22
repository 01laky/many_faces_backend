/*
 * UserProfileTests.cs - Unit tests for UserProfile functionality
 * 
 * Tests that UserProfile is automatically created on user registration
 * and that one-to-one relationship works correctly.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for UserProfile entity and registration flow
/// </summary>
public class UserProfileTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserProfileTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldCreateUserProfile_WhenUserIsRegistered()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Act - Register user
        var registerResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        // Assert - Registration should succeed
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        responseJson.GetProperty("userId").GetString().Should().NotBeNullOrEmpty();
        responseJson.GetProperty("profileId").GetInt32().Should().BeGreaterThan(0);

        // Verify UserProfile was created in database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = responseJson.GetProperty("userId").GetString() ?? string.Empty;
            var userProfile = await context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            userProfile.Should().NotBeNull();
            userProfile!.UserId.Should().Be(userId);
            userProfile.Id.Should().BeGreaterThan(0); // Auto-increment ID
        }
    }

    [Fact]
    public async Task UserProfile_ShouldHaveOneToOneRelationship_WithApplicationUser()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Act - Register user
        var registerResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = responseJson.GetProperty("userId").GetString() ?? string.Empty;

        // Assert - Verify one-to-one relationship
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Load user with profile
            var user = await context.Users
                .Include(u => u.UserProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            user.Should().NotBeNull();
            user!.UserProfile.Should().NotBeNull();
            user.UserProfile!.UserId.Should().Be(userId);

            // Verify UserProfile.User navigation property works
            var profile = await context.UserProfiles
                .Include(up => up.User)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            profile.Should().NotBeNull();
            profile!.User.Should().NotBeNull();
            profile.User.Id.Should().Be(userId);
        }
    }

    [Fact]
    public async Task UserProfile_ShouldHaveAutoIncrementId()
    {
        // Arrange
        var email1 = $"test_{Guid.NewGuid()}@test.com";
        var email2 = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Act - Register two users
        var response1 = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email = email1,
            password,
            firstName = "User",
            lastName = "One"
        });
        var response2 = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email = email2,
            password,
            firstName = "User",
            lastName = "Two"
        });

        // Assert - Both registrations should succeed
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var json1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var json2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        var profileId1 = json1.GetProperty("profileId").GetInt32();
        var profileId2 = json2.GetProperty("profileId").GetInt32();

        // Profile IDs should be different and auto-incremented
        profileId1.Should().NotBe(profileId2);
        profileId2.Should().BeGreaterThan(profileId1);
    }

    [Fact]
    public async Task UserProfile_ShouldBeCreatedWithDefaultValues()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Act - Register user
        var registerResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = responseJson.GetProperty("userId").GetString() ?? string.Empty;

        // Assert - UserProfile should have default values
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userProfile = await context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            userProfile.Should().NotBeNull();
            userProfile!.UserId.Should().Be(userId);
            userProfile.Nickname.Should().BeNull(); // Not set during registration
            userProfile.Age.Should().BeNull(); // Not set during registration
            userProfile.Rod.Should().BeNull(); // Not set during registration
            userProfile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
