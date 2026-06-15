using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Scripts;

namespace BeDemo.Api.Tests;

/// <summary>
/// Regression guard for the post-login redirect bug: a seeded user with no <see cref="UserFaceRole"/> is never
/// returned a private face by <c>FacesConfigService</c>, so the SPA could not redirect off <c>/public/home</c>.
/// <see cref="DatabaseSeeder.EnsureUserFaceProvisioningAsync"/> must backfill the per-face profile + role for
/// pre-existing users and stay idempotent on re-run (the seeder runs on every startup).
/// </summary>
public class DatabaseSeederFaceProvisioningTests
{
	private static ApplicationDbContext NewContext() =>
		new(new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"seed_provisioning_{Guid.NewGuid()}")
			.Options);

	private static (Face Public, Face Basic) SeedTwoFaces(ApplicationDbContext ctx)
	{
		var publicFace = new Face { Index = "public", Title = "Public", IsPublic = true, CreatedAt = DateTime.UtcNow };
		var basicFace = new Face { Index = "basic", Title = "Basic", IsPublic = false, CreatedAt = DateTime.UtcNow };
		ctx.Faces.AddRange(publicFace, basicFace);
		ctx.SaveChanges();
		return (publicFace, basicFace);
	}

	private static UserRole SeedFaceUserRole(ApplicationDbContext ctx)
	{
		var role = new UserRole { Name = UserRole.FaceRoleNames.FaceUser, Scope = RoleScope.Face, CreatedAt = DateTime.UtcNow };
		ctx.UserRoles.Add(role);
		ctx.SaveChanges();
		return role;
	}

	[Fact]
	public async Task EnsureUserFaceProvisioning_backfills_profile_and_role_for_every_face()
	{
		await using var ctx = NewContext();
		var (publicFace, basicFace) = SeedTwoFaces(ctx);
		var role = SeedFaceUserRole(ctx);
		var user = new ApplicationUser { Id = "u1", UserName = "user01@demo.com", Email = "user01@demo.com" };
		ctx.Users.Add(user);
		await ctx.SaveChangesAsync();

		await DatabaseSeeder.EnsureUserFaceProvisioningAsync(
			ctx, user, displayName: "Ján Horváth", nickname: "jan.horvath", age: 25, rod: "M",
			faces: new[] { publicFace, basicFace }, resolveRoleId: _ => role.Id);

		(await ctx.UserProfiles.CountAsync(p => p.UserId == "u1")).Should().Be(1);

		var faceRoleIds = await ctx.UserFaceRoles.Where(r => r.UserId == "u1").Select(r => r.FaceId).ToListAsync();
		faceRoleIds.Should().BeEquivalentTo(new[] { publicFace.Id, basicFace.Id },
			"a UserFaceRole on the private face is what makes faces/config return it after login");

		var profileId = await ctx.UserProfiles.Where(p => p.UserId == "u1").Select(p => p.Id).FirstAsync();
		var faceProfileIds = await ctx.UserFaceProfiles.Where(p => p.UserProfileId == profileId).Select(p => p.FaceId).ToListAsync();
		faceProfileIds.Should().BeEquivalentTo(new[] { publicFace.Id, basicFace.Id });
	}

	[Fact]
	public async Task EnsureUserFaceProvisioning_is_idempotent_on_rerun()
	{
		await using var ctx = NewContext();
		var (publicFace, basicFace) = SeedTwoFaces(ctx);
		var role = SeedFaceUserRole(ctx);
		var user = new ApplicationUser { Id = "u1", UserName = "user01@demo.com", Email = "user01@demo.com" };
		ctx.Users.Add(user);
		await ctx.SaveChangesAsync();

		// Re-running must not throw (UserFaceRole PK is (UserId, FaceId)) nor create duplicates.
		for (var run = 0; run < 3; run++)
		{
			await DatabaseSeeder.EnsureUserFaceProvisioningAsync(
				ctx, user, displayName: "Ján Horváth", nickname: "jan.horvath", age: 25, rod: "M",
				faces: new[] { publicFace, basicFace }, resolveRoleId: _ => role.Id);
		}

		(await ctx.UserProfiles.CountAsync(p => p.UserId == "u1")).Should().Be(1);
		(await ctx.UserFaceRoles.CountAsync(r => r.UserId == "u1")).Should().Be(2);
		(await ctx.UserFaceProfiles.CountAsync()).Should().Be(2);
	}

	[Fact]
	public async Task EnsureUserFaceProvisioning_adds_missing_role_when_only_face_profile_exists()
	{
		// Mirrors the broken DB state: user had a single UserFaceProfile (e.g. from a runtime visit) but zero roles.
		await using var ctx = NewContext();
		var (publicFace, basicFace) = SeedTwoFaces(ctx);
		var role = SeedFaceUserRole(ctx);
		var user = new ApplicationUser { Id = "u1", UserName = "user01@demo.com", Email = "user01@demo.com" };
		ctx.Users.Add(user);
		var profile = new UserProfile { UserId = "u1", CreatedAt = DateTime.UtcNow };
		ctx.UserProfiles.Add(profile);
		await ctx.SaveChangesAsync();
		ctx.UserFaceProfiles.Add(new UserFaceProfile
		{
			UserProfileId = profile.Id,
			FaceId = publicFace.Id,
			IsActive = false,
			Visited = true,
			FaceRoleIntroCompleted = false,
			CreatedAt = DateTime.UtcNow,
		});
		await ctx.SaveChangesAsync();

		await DatabaseSeeder.EnsureUserFaceProvisioningAsync(
			ctx, user, displayName: "Ján Horváth", nickname: "jan.horvath", age: 25, rod: "M",
			faces: new[] { publicFace, basicFace }, resolveRoleId: _ => role.Id);

		// Existing public face profile reused (not duplicated), basic one added, both roles created.
		(await ctx.UserFaceProfiles.CountAsync()).Should().Be(2);
		(await ctx.UserFaceRoles.CountAsync(r => r.UserId == "u1")).Should().Be(2);
	}

	[Fact]
	public async Task EnsureUserFaceProvisioning_skips_role_when_resolver_returns_null()
	{
		await using var ctx = NewContext();
		var (publicFace, basicFace) = SeedTwoFaces(ctx);
		var user = new ApplicationUser { Id = "u1", UserName = "x@demo.com", Email = "x@demo.com" };
		ctx.Users.Add(user);
		await ctx.SaveChangesAsync();

		await DatabaseSeeder.EnsureUserFaceProvisioningAsync(
			ctx, user, displayName: "X", nickname: "x", age: 30, rod: "M",
			faces: new[] { publicFace, basicFace }, resolveRoleId: _ => null);

		(await ctx.UserFaceProfiles.CountAsync()).Should().Be(2);
		(await ctx.UserFaceRoles.CountAsync()).Should().Be(0);
	}
}
