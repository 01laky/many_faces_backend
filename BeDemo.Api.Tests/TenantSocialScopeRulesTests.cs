using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class TenantSocialScopeRulesTests
{
	[Fact]
	public async Task BothUsersParticipateInFaceAsync_True_When_Both_Have_FaceProfile()
	{
		await using var ctx = NewContext();
		var faceId = 99;
		await ctx.Users.AddRangeAsync(
			new ApplicationUser { Id = "a", UserName = "a@test", Email = "a@test", EmailConfirmed = true, UserRoleId = 1 },
			new ApplicationUser { Id = "b", UserName = "b@test", Email = "b@test", EmailConfirmed = true, UserRoleId = 1 });
		await ctx.UserProfiles.AddRangeAsync(
			new UserProfile { UserId = "a" },
			new UserProfile { UserId = "b" });
		await ctx.SaveChangesAsync();
		var upA = await ctx.UserProfiles.SingleAsync(p => p.UserId == "a");
		var upB = await ctx.UserProfiles.SingleAsync(p => p.UserId == "b");
		ctx.UserFaceProfiles.AddRange(
			new UserFaceProfile { UserProfileId = upA.Id, FaceId = faceId },
			new UserFaceProfile { UserProfileId = upB.Id, FaceId = faceId });
		await ctx.SaveChangesAsync();

		(await TenantSocialScopeRules.BothUsersParticipateInFaceAsync(ctx, faceId, "a", "b")).Should().BeTrue();
	}

	[Fact]
	public async Task BothUsersParticipateInFaceAsync_False_When_One_Missing_FaceProfile()
	{
		await using var ctx = NewContext();
		var faceId = 99;
		await ctx.Users.AddRangeAsync(
			new ApplicationUser { Id = "a", UserName = "a@test", Email = "a@test", EmailConfirmed = true, UserRoleId = 1 },
			new ApplicationUser { Id = "b", UserName = "b@test", Email = "b@test", EmailConfirmed = true, UserRoleId = 1 });
		await ctx.UserProfiles.AddRangeAsync(
			new UserProfile { UserId = "a" },
			new UserProfile { UserId = "b" });
		await ctx.SaveChangesAsync();
		var upA = await ctx.UserProfiles.SingleAsync(p => p.UserId == "a");
		ctx.UserFaceProfiles.Add(new UserFaceProfile { UserProfileId = upA.Id, FaceId = faceId });
		await ctx.SaveChangesAsync();

		(await TenantSocialScopeRules.BothUsersParticipateInFaceAsync(ctx, faceId, "a", "b")).Should().BeFalse();
	}

	private static ApplicationDbContext NewContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}
}
