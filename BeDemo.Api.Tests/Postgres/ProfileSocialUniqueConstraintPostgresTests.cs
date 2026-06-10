using BeDemo.Api.Data;
using BeDemo.Api.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>
/// Postgres-lane checks for the profile-social unique indexes the InMemory provider cannot enforce (backend-refactor
/// Phase 4): one like per (UserFaceProfileId, UserId) on <see cref="UserFaceProfileLike"/> and one review per
/// (UserFaceProfileId, AuthorUserId) on <see cref="UserFaceProfileReview"/> — i.e. a user cannot double-like or
/// double-review the same face profile.
/// </summary>
[Trait("Category", "Postgres")]
[Collection("Postgres")]
public sealed class ProfileSocialUniqueConstraintPostgresTests
{
	private readonly PostgresFixture _pg;

	public ProfileSocialUniqueConstraintPostgresTests(PostgresFixture pg) => _pg = pg;

	/// <summary>Seeds role → user → profile → face → user-face-profile and returns (ufpId, userId).</summary>
	private static async Task<(int UfpId, string UserId)> SeedUserFaceProfileAsync(ApplicationDbContext ctx)
	{
		var role = new UserRole { Name = UserRole.GlobalRoleNames.User };
		ctx.UserRoles.Add(role);
		await ctx.SaveChangesAsync();

		var user = new ApplicationUser
		{
			Id = Guid.NewGuid().ToString("N"),
			UserName = "social@test.com",
			Email = "social@test.com",
			UserRoleId = role.Id,
		};
		var profile = new UserProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
		var face = new Face { Index = "face-" + Guid.NewGuid().ToString("N"), Title = "Social Face" };
		ctx.Users.Add(user);
		ctx.UserProfiles.Add(profile);
		ctx.Faces.Add(face);
		await ctx.SaveChangesAsync();

		var ufp = new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = face.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
		};
		ctx.UserFaceProfiles.Add(ufp);
		await ctx.SaveChangesAsync();
		return (ufp.Id, user.Id);
	}

	[Fact]
	public async Task Duplicate_like_per_user_violates_unique_index()
	{
		await using var ctx = await _pg.CreateContextInNewDatabaseAsync("like_" + Guid.NewGuid().ToString("N"));
		await ctx.Database.EnsureCreatedAsync();
		var (ufpId, userId) = await SeedUserFaceProfileAsync(ctx);

		ctx.UserFaceProfileLikes.Add(new UserFaceProfileLike { UserFaceProfileId = ufpId, UserId = userId });
		await ctx.SaveChangesAsync();

		ctx.UserFaceProfileLikes.Add(new UserFaceProfileLike { UserFaceProfileId = ufpId, UserId = userId });
		var act = async () => await ctx.SaveChangesAsync();
		(await act.Should().ThrowAsync<DbUpdateException>())
			.Which.InnerException!.Message.Should().MatchEquivalentOf("*duplicate*");
	}

	[Fact]
	public async Task Duplicate_review_per_author_violates_unique_index()
	{
		await using var ctx = await _pg.CreateContextInNewDatabaseAsync("review_" + Guid.NewGuid().ToString("N"));
		await ctx.Database.EnsureCreatedAsync();
		var (ufpId, userId) = await SeedUserFaceProfileAsync(ctx);

		ctx.UserFaceProfileReviews.Add(new UserFaceProfileReview
		{
			UserFaceProfileId = ufpId,
			AuthorUserId = userId,
			Title = "First",
			Text = "Original review",
			Stars = 5,
		});
		await ctx.SaveChangesAsync();

		ctx.UserFaceProfileReviews.Add(new UserFaceProfileReview
		{
			UserFaceProfileId = ufpId,
			AuthorUserId = userId,
			Title = "Second",
			Text = "Should be rejected",
			Stars = 1,
		});
		var act = async () => await ctx.SaveChangesAsync();
		(await act.Should().ThrowAsync<DbUpdateException>())
			.Which.InnerException!.Message.Should().MatchEquivalentOf("*duplicate*");
	}
}
