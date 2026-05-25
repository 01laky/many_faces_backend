using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class StoryViewerRulesTests
{
	[Fact]
	public async Task ViewerHasFaceMembershipAsync_ShouldBeFalse_WhenNoAssignment()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"story_rules_{Guid.NewGuid()}")
			.Options;
		await using var ctx = new ApplicationDbContext(options);
		var ok = await StoryViewerRules.ViewerHasFaceMembershipAsync(ctx, "user-1", 99);
		ok.Should().BeFalse();
	}

	[Fact]
	public async Task ViewerHasFaceMembershipAsync_ShouldBeTrue_WhenUserFaceRoleExists()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"story_rules_{Guid.NewGuid()}")
			.Options;
		await using var ctx = new ApplicationDbContext(options);
		ctx.Faces.Add(new Face
		{
			Index = "t",
			Title = "T",
			IsPublic = true,
			CreatedAt = DateTime.UtcNow,
		});
		ctx.UserRoles.Add(new UserRole
		{
			Name = UserRole.FaceRoleNames.FaceHost,
			Scope = RoleScope.Face,
			CreatedAt = DateTime.UtcNow,
		});
		await ctx.SaveChangesAsync();
		var face = await ctx.Faces.SingleAsync();
		var role = await ctx.UserRoles.SingleAsync();
		ctx.UserFaceRoles.Add(new UserFaceRole
		{
			UserId = "u1",
			FaceId = face.Id,
			UserRoleId = role.Id,
			CreatedAt = DateTime.UtcNow,
		});
		await ctx.SaveChangesAsync();

		var ok = await StoryViewerRules.ViewerHasFaceMembershipAsync(ctx, "u1", face.Id);
		ok.Should().BeTrue();
	}
}
