/*
 * UserFaceProfileTests.cs - Unit tests for UserFaceProfile functionality
 *
 * Tests that UserFaceProfile is automatically created for all Faces when user registers
 * and that many-to-one relationships work correctly.
 */

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

	private async Task<int> RegisterAndGetProfileIdAsync()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		var completed = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			"Test1234!@##",
			"Test",
			"User");

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var profile = await context.UserProfiles.FirstAsync(up => up.UserId == completed.UserId);
		return profile.Id;
	}

	[Fact]
	public async Task Register_ShouldCreateUserFaceProfiles_ForAllFaces_WhenUserIsRegistered()
	{
		using (var scope = _factory.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var facesCount = await context.Faces.CountAsync();
			facesCount.Should().BeGreaterThan(0, "At least one Face should exist for testing");
		}

		var profileId = await RegisterAndGetProfileIdAsync();

		using (var scope = _factory.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var totalFaces = await context.Faces.CountAsync();
			totalFaces.Should().BeGreaterThan(0);

			var userFaceProfiles = await context.UserFaceProfiles
				.Include(ufp => ufp.Face)
				.Include(ufp => ufp.UserProfile)
				.Where(ufp => ufp.UserProfileId == profileId)
				.ToListAsync();

			userFaceProfiles.Should().HaveCount(totalFaces, "One UserFaceProfile should exist for each Face");

			foreach (var userFaceProfile in userFaceProfiles)
			{
				userFaceProfile.UserProfileId.Should().Be(profileId);
				userFaceProfile.FaceId.Should().BeGreaterThan(0);
				userFaceProfile.Face.Should().NotBeNull();
				userFaceProfile.UserProfile.Should().NotBeNull();
				userFaceProfile.IsActive.Should().BeFalse("FACE_HOST is not directory-active");
				userFaceProfile.Visited.Should().BeFalse();
				userFaceProfile.FaceRoleIntroCompleted.Should().BeFalse();
				userFaceProfile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
			}
		}
	}

	[Fact]
	public async Task UserFaceProfile_ShouldHaveManyToOneRelationship_WithUserProfile()
	{
		var profileId = await RegisterAndGetProfileIdAsync();

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var userProfile = await context.UserProfiles
			.Include(up => up.UserFaceProfiles)
			.ThenInclude(ufp => ufp.Face)
			.FirstOrDefaultAsync(up => up.Id == profileId);

		userProfile.Should().NotBeNull();
		userProfile!.UserFaceProfiles.Should().NotBeNullOrEmpty();
		userProfile.UserFaceProfiles.Count.Should().BeGreaterThan(0);

		foreach (var userFaceProfile in userProfile.UserFaceProfiles)
		{
			userFaceProfile.UserProfile.Should().NotBeNull();
			userFaceProfile.UserProfile.Id.Should().Be(profileId);
			userFaceProfile.Face.Should().NotBeNull();
			userFaceProfile.Face.Id.Should().BeGreaterThan(0);
		}
	}

	[Fact]
	public async Task UserFaceProfile_ShouldHaveManyToOneRelationship_WithFace()
	{
		var profileId = await RegisterAndGetProfileIdAsync();

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var face = await context.Faces
			.Include(f => f.UserFaceProfiles)
			.ThenInclude(ufp => ufp.UserProfile)
			.FirstOrDefaultAsync();

		face.Should().NotBeNull();

		var userFaceProfileForThisFace = face!.UserFaceProfiles
			.FirstOrDefault(ufp => ufp.UserProfileId == profileId);

		userFaceProfileForThisFace.Should().NotBeNull();
		userFaceProfileForThisFace!.Face.Should().NotBeNull();
		userFaceProfileForThisFace.Face.Id.Should().Be(face.Id);
		userFaceProfileForThisFace.UserProfile.Should().NotBeNull();
		userFaceProfileForThisFace.UserProfile.Id.Should().Be(profileId);
	}

	[Fact(Skip = "InMemory provider does not enforce unique constraints; use PostgreSQL to verify.")]
	public async Task UserFaceProfile_ShouldHaveUniqueConstraint_OnUserProfileIdAndFaceId()
	{
		var profileId = await RegisterAndGetProfileIdAsync();

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var face = await context.Faces.FirstOrDefaultAsync();
		face.Should().NotBeNull();

		var duplicateProfile = new UserFaceProfile
		{
			UserProfileId = profileId,
			FaceId = face!.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.UserFaceProfiles.Add(duplicateProfile);

		var action = async () => await context.SaveChangesAsync();
		await action.Should().ThrowAsync<DbUpdateException>()
			.Where(ex => (ex.InnerException != null && (ex.InnerException.Message.Contains("duplicate") || ex.InnerException.Message.Contains("unique"))) ||
						 ex.Message.Contains("duplicate") ||
						 ex.Message.Contains("unique"));
	}

	[Fact]
	public async Task UserFaceProfile_ShouldBeCreatedWithDefaultValues()
	{
		var profileId = await RegisterAndGetProfileIdAsync();

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var userFaceProfile = await context.UserFaceProfiles
			.FirstOrDefaultAsync(ufp => ufp.UserProfileId == profileId);

		userFaceProfile.Should().NotBeNull();
		userFaceProfile!.DisplayName.Should().BeNull();
		userFaceProfile.AvatarUrl.Should().BeNull();
		userFaceProfile.Settings.Should().BeNull();
		userFaceProfile.IsActive.Should().BeFalse();
		userFaceProfile.Visited.Should().BeFalse();
		userFaceProfile.FaceRoleIntroCompleted.Should().BeFalse();
		userFaceProfile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		userFaceProfile.UpdatedAt.Should().BeNull();
	}

	public void Dispose() => _client.Dispose();
}
