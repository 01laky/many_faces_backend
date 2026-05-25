using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests;

public class FaceModerationServiceTests
{
	[Fact]
	public async Task IsUserBannedFromFaceAsync_ShouldReflectActiveRowsOnly()
	{
		await using var context = CreateContext();
		var service = new FaceModerationService(context);
		const string userId = "user-1";
		context.UserFaceModerations.AddRange(
			new UserFaceModeration
			{
				UserId = userId,
				FaceId = 1,
				BannedByUserId = "op",
				Reason = "first ban",
				BannedAt = DateTime.UtcNow,
				LiftedAt = DateTime.UtcNow,
			},
			new UserFaceModeration
			{
				UserId = userId,
				FaceId = 1,
				BannedByUserId = "op",
				Reason = "active ban",
				BannedAt = DateTime.UtcNow,
			});
		await context.SaveChangesAsync();

		(await service.IsUserBannedFromFaceAsync(userId, 1)).Should().BeTrue();
		(await service.IsUserBannedFromFaceAsync(userId, 2)).Should().BeFalse();
	}

	[Fact]
	public void IsUserGloballyBanned_ShouldUseLockoutEnd()
	{
		var service = new FaceModerationService(CreateContext());
		var banned = new ApplicationUser
		{
			LockoutEnabled = true,
			LockoutEnd = DateTimeOffset.UtcNow.AddDays(1),
		};
		var enabledWithoutEnd = new ApplicationUser { LockoutEnabled = true, LockoutEnd = null };
		var notBanned = new ApplicationUser { LockoutEnabled = false };
		service.IsUserGloballyBanned(enabledWithoutEnd).Should().BeFalse();
		service.IsUserGloballyBanned(banned).Should().BeTrue();
		service.IsUserGloballyBanned(notBanned).Should().BeFalse();
	}

	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"face-moderation-{Guid.NewGuid():N}")
			.Options;
		var context = new ApplicationDbContext(options);
		context.Database.EnsureCreated();
		return context;
	}
}
