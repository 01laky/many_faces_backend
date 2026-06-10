using BeDemo.Api.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>
/// Postgres-backed version of the unique-constraint check the InMemory provider cannot make (backend-refactor Phase 4 —
/// replaces the <c>[Fact(Skip = …)]</c> in <see cref="UserFaceProfileTests"/>). Verifies the real database rejects a
/// second <see cref="UserFaceProfile"/> for the same (UserProfileId, FaceId) pair (ApplicationDbContext.cs ~line 244).
/// </summary>
[Trait("Category", "Postgres")]
[Collection("Postgres")]
public sealed class UserFaceProfileUniqueConstraintPostgresTests
{
	private readonly PostgresFixture _pg;

	public UserFaceProfileUniqueConstraintPostgresTests(PostgresFixture pg) => _pg = pg;

	[Fact]
	public async Task Duplicate_UserProfileId_FaceId_violates_unique_index()
	{
		await using var ctx = await _pg.CreateContextInNewDatabaseAsync("ufp_" + Guid.NewGuid().ToString("N"));
		await ctx.Database.EnsureCreatedAsync();

		// ApplicationUser has a required UserRoleId FK → seed a UserRole first.
		var role = new UserRole { Name = UserRole.GlobalRoleNames.User };
		ctx.UserRoles.Add(role);
		await ctx.SaveChangesAsync();

		var user = new ApplicationUser
		{
			Id = Guid.NewGuid().ToString("N"),
			UserName = "ufp@test.com",
			Email = "ufp@test.com",
			UserRoleId = role.Id,
		};
		var profile = new UserProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
		var face = new Face { Index = "face-" + Guid.NewGuid().ToString("N"), Title = "Constraint Face" };
		ctx.Users.Add(user);
		ctx.UserProfiles.Add(profile);
		ctx.Faces.Add(face);
		await ctx.SaveChangesAsync();

		ctx.UserFaceProfiles.Add(new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
		});
		await ctx.SaveChangesAsync();

		// Second row for the same (UserProfileId, FaceId) must be rejected by the unique index.
		ctx.UserFaceProfiles.Add(new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
		});

		var act = async () => await ctx.SaveChangesAsync();
		(await act.Should().ThrowAsync<DbUpdateException>())
			.Which.InnerException!.Message.Should().MatchEquivalentOf("*duplicate*");
	}
}
