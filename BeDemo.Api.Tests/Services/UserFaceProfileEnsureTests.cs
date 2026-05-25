using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests.Services;

/// <summary>BE-RA13…RA18 — shared UserFaceProfile create-or-get paths.</summary>
public sealed class UserFaceProfileEnsureTests
{
	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	private static async Task<(UserProfile Profile, Face Face)> SeedProfileAndFaceAsync(ApplicationDbContext context)
	{
		var profile = new UserProfile { UserId = "user-1" };
		var face = new Face { Index = "demo", Title = "Demo", IsPublic = true, CreatedAt = DateTime.UtcNow };
		context.UserProfiles.Add(profile);
		context.Faces.Add(face);
		await context.SaveChangesAsync();
		return (profile, face);
	}

	[Fact]
	public async Task BE_RA13_Passive_CreatesRowWithDefaults()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.Passive);
		await context.SaveChangesAsync();

		ufp.UserProfileId.Should().Be(profile.Id);
		ufp.FaceId.Should().Be(face.Id);
		ufp.IsActive.Should().BeFalse();
		ufp.Visited.Should().BeFalse();
		ufp.FaceRoleIntroCompleted.Should().BeFalse();
		ufp.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		(await context.UserFaceProfiles.CountAsync()).Should().Be(1);
	}

	[Fact]
	public async Task BE_RA14_Passive_ReturnsExistingWithoutOverwritingFlags()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);
		var existing = new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = true,
			Visited = true,
			FaceRoleIntroCompleted = true,
			CreatedAt = DateTime.UtcNow.AddDays(-1),
		};
		context.UserFaceProfiles.Add(existing);
		await context.SaveChangesAsync();

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.Passive);

		ufp.Id.Should().Be(existing.Id);
		ufp.IsActive.Should().BeTrue();
		ufp.Visited.Should().BeTrue();
		ufp.FaceRoleIntroCompleted.Should().BeTrue();
		(await context.UserFaceProfiles.CountAsync()).Should().Be(1);
	}

	[Fact]
	public async Task BE_RA15_ForVisit_CreatesVisitedRow()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.ForVisit);
		await context.SaveChangesAsync();

		ufp.Visited.Should().BeTrue();
		ufp.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task BE_RA16_ForVisit_UpdatesExistingToVisitedOnly()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);
		var existing = new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = true,
			Visited = false,
			FaceRoleIntroCompleted = false,
			CreatedAt = DateTime.UtcNow.AddDays(-1),
		};
		context.UserFaceProfiles.Add(existing);
		await context.SaveChangesAsync();

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.ForVisit);

		ufp.Visited.Should().BeTrue();
		ufp.IsActive.Should().BeTrue();
		ufp.UpdatedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task BE_RA17_ForFaceRole_SetsActiveAndIntroFlags()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.ForFaceRole(isActive: true, faceRoleIntroCompleted: true));
		await context.SaveChangesAsync();

		ufp.IsActive.Should().BeTrue();
		ufp.FaceRoleIntroCompleted.Should().BeTrue();
		ufp.Visited.Should().BeFalse();
	}

	[Fact]
	public async Task BE_RA18_ForFaceRole_UpdatesExistingRow()
	{
		await using var context = CreateContext();
		var (profile, face) = await SeedProfileAndFaceAsync(context);
		var existing = new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = false,
			Visited = true,
			FaceRoleIntroCompleted = false,
			CreatedAt = DateTime.UtcNow.AddDays(-1),
		};
		context.UserFaceProfiles.Add(existing);
		await context.SaveChangesAsync();

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			context,
			profile.Id,
			face.Id,
			UserFaceProfileEnsure.Options.ForFaceRole(isActive: true, faceRoleIntroCompleted: true));

		ufp.Id.Should().Be(existing.Id);
		ufp.IsActive.Should().BeTrue();
		ufp.FaceRoleIntroCompleted.Should().BeTrue();
		ufp.Visited.Should().BeTrue();
	}
}
