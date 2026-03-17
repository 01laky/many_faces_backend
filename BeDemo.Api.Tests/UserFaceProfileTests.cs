/*
 * UserFaceProfileTests.cs - Unit tests for UserFaceProfile functionality
 * 
 * Tests that UserFaceProfile is automatically created for all Faces when user registers
 * and that many-to-one relationships work correctly.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for UserFaceProfile entity and registration flow
/// </summary>
public class UserFaceProfileTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserFaceProfileTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldCreateUserFaceProfiles_ForAllFaces_WhenUserIsRegistered()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Get count of existing faces before registration
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var facesCount = await context.Faces.CountAsync();
            facesCount.Should().BeGreaterThan(0, "At least one Face should exist for testing");
        }

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
        var profileId = responseJson.GetProperty("profileId").GetInt32();
        var faceProfileCount = responseJson.GetProperty("faceProfileCount").GetInt32();

        // Verify UserFaceProfiles were created in database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Count total faces
            var totalFaces = await context.Faces.CountAsync();
            totalFaces.Should().BeGreaterThan(0);
            faceProfileCount.Should().Be(totalFaces, "One UserFaceProfile should be created for each Face");

            // Get all UserFaceProfiles for this user
            var userFaceProfiles = await context.UserFaceProfiles
                .Include(ufp => ufp.Face)
                .Include(ufp => ufp.UserProfile)
                .Where(ufp => ufp.UserProfileId == profileId)
                .ToListAsync();

            userFaceProfiles.Should().HaveCount(totalFaces, "One UserFaceProfile should exist for each Face");

            // Verify each UserFaceProfile has correct UserProfileId and FaceId
            foreach (var userFaceProfile in userFaceProfiles)
            {
                userFaceProfile.UserProfileId.Should().Be(profileId);
                userFaceProfile.FaceId.Should().BeGreaterThan(0);
                userFaceProfile.Face.Should().NotBeNull();
                userFaceProfile.UserProfile.Should().NotBeNull();
                userFaceProfile.IsActive.Should().BeTrue();
                userFaceProfile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            }
        }
    }

    [Fact]
    public async Task UserFaceProfile_ShouldHaveManyToOneRelationship_WithUserProfile()
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
        var profileId = responseJson.GetProperty("profileId").GetInt32();

        // Assert - Verify many-to-one relationship: UserProfile -> UserFaceProfiles
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Load UserProfile with UserFaceProfiles
            var userProfile = await context.UserProfiles
                .Include(up => up.UserFaceProfiles)
                .ThenInclude(ufp => ufp.Face)
                .FirstOrDefaultAsync(up => up.Id == profileId);

            userProfile.Should().NotBeNull();
            userProfile!.UserFaceProfiles.Should().NotBeNullOrEmpty();
            userProfile.UserFaceProfiles.Count.Should().BeGreaterThan(0);

            // Verify navigation properties work
            foreach (var userFaceProfile in userProfile.UserFaceProfiles)
            {
                userFaceProfile.UserProfile.Should().NotBeNull();
                userFaceProfile.UserProfile.Id.Should().Be(profileId);
                userFaceProfile.Face.Should().NotBeNull();
                userFaceProfile.Face.Id.Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task UserFaceProfile_ShouldHaveManyToOneRelationship_WithFace()
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
        var profileId = responseJson.GetProperty("profileId").GetInt32();

        // Assert - Verify many-to-one relationship: Face -> UserFaceProfiles
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get first face
            var face = await context.Faces
                .Include(f => f.UserFaceProfiles)
                .ThenInclude(ufp => ufp.UserProfile)
                .FirstOrDefaultAsync();

            face.Should().NotBeNull();

            // Verify that at least one UserFaceProfile exists for this face (for our registered user)
            var userFaceProfileForThisFace = face!.UserFaceProfiles
                .FirstOrDefault(ufp => ufp.UserProfileId == profileId);

            userFaceProfileForThisFace.Should().NotBeNull();
            userFaceProfileForThisFace!.Face.Should().NotBeNull();
            userFaceProfileForThisFace.Face.Id.Should().Be(face.Id);
            userFaceProfileForThisFace.UserProfile.Should().NotBeNull();
            userFaceProfileForThisFace.UserProfile.Id.Should().Be(profileId);
        }
    }

    [Fact(Skip = "InMemory provider does not enforce unique constraints; use PostgreSQL to verify.")]
    public async Task UserFaceProfile_ShouldHaveUniqueConstraint_OnUserProfileIdAndFaceId()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Act - Register user (creates UserFaceProfiles automatically)
        var registerResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var profileId = responseJson.GetProperty("profileId").GetInt32();

        // Try to create duplicate UserFaceProfile (should fail due to unique constraint)
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get first face
            var face = await context.Faces.FirstOrDefaultAsync();
            face.Should().NotBeNull();

            // Try to create duplicate UserFaceProfile
            var duplicateProfile = new UserFaceProfile
            {
                UserProfileId = profileId,
                FaceId = face!.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.UserFaceProfiles.Add(duplicateProfile);

            // Should throw exception due to unique constraint (InMemory does not enforce it; skip assertion when not using PostgreSQL)
            var action = async () => await context.SaveChangesAsync();
            await action.Should().ThrowAsync<DbUpdateException>()
                .Where(ex => (ex.InnerException != null && (ex.InnerException.Message.Contains("duplicate") || ex.InnerException.Message.Contains("unique"))) ||
                             ex.Message.Contains("duplicate") ||
                             ex.Message.Contains("unique"));
        }
    }

    [Fact]
    public async Task UserFaceProfile_ShouldBeCreatedWithDefaultValues()
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
        var profileId = responseJson.GetProperty("profileId").GetInt32();

        // Assert - UserFaceProfile should have default values
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userFaceProfile = await context.UserFaceProfiles
                .FirstOrDefaultAsync(ufp => ufp.UserProfileId == profileId);

            userFaceProfile.Should().NotBeNull();
            userFaceProfile!.DisplayName.Should().BeNull(); // Not set during registration
            userFaceProfile.AvatarUrl.Should().BeNull(); // Not set during registration
            userFaceProfile.Settings.Should().BeNull(); // Not set during registration
            userFaceProfile.IsActive.Should().BeTrue(); // Default value
            userFaceProfile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            userFaceProfile.UpdatedAt.Should().BeNull(); // Not updated yet
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
