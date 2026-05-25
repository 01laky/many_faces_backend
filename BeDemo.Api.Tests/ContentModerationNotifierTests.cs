using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for <see cref="ContentModerationNotifier"/> edge cases (empty creator id, super-admin fan-out).
/// </summary>
public sealed class ContentModerationNotifierTests
{
	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"notifier-{Guid.NewGuid():N}")
			.Options;
		var context = new ApplicationDbContext(options);
		context.Database.EnsureCreated();
		return context;
	}

	[Fact]
	public void NotifyCreator_ShouldNotAddRows_WhenCreatorIdIsMissing()
	{
		using var context = CreateContext();
		var notifier = new ContentModerationNotifier(context);

		notifier.NotifyCreator("", "t", "m");
		notifier.NotifyCreator("   ", "t", "m");
		notifier.NotifyCreator(null!, "t", "m");

		context.SaveChanges();
		context.Notifications.Should().BeEmpty();
	}

	[Fact]
	public void NotifyCreator_ShouldPersistNotification()
	{
		using var context = CreateContext();
		SeedRoles(context);
		var creator = NewUser("creator-1", UserRole.GlobalRoleNames.User);
		context.Users.Add(creator);
		context.SaveChanges();

		var notifier = new ContentModerationNotifier(context);
		notifier.NotifyCreator(creator.Id, "Submitted", "Your blog is pending review.");
		context.SaveChanges();

		var n = context.Notifications.Single();
		n.UserId.Should().Be(creator.Id);
		n.Title.Should().Be("Submitted");
		n.Type.Should().Be("content_moderation");
	}

	[Fact]
	public async Task NotifySuperAdminsAsync_ShouldWriteOneRowPerSuperAdmin()
	{
		await using var context = CreateContext();
		SeedRoles(context);
		context.Users.AddRange(
			NewUser("sa-1", UserRole.GlobalRoleNames.SuperAdmin),
			NewUser("sa-2", UserRole.GlobalRoleNames.SuperAdmin),
			NewUser("user-1", UserRole.GlobalRoleNames.User));
		await context.SaveChangesAsync();

		var notifier = new ContentModerationNotifier(context);
		await notifier.NotifySuperAdminsAsync("Queue alert", "AI jobs failing", "moderation_ops");
		await context.SaveChangesAsync();

		context.Notifications.Should().HaveCount(2);
		context.Notifications.Should().OnlyContain(n => n.Type == "moderation_ops");
		context.Notifications.Select(n => n.UserId).Distinct().Should().HaveCount(2);
		context.Notifications.Should().OnlyContain(n => n.UserId.StartsWith("sa-1-", StringComparison.Ordinal) || n.UserId.StartsWith("sa-2-", StringComparison.Ordinal));
	}

	[Fact]
	public async Task NotifySuperAdminsAsync_ShouldWriteNothing_WhenNoSuperAdminsExist()
	{
		await using var context = CreateContext();
		SeedRoles(context);
		context.Users.Add(NewUser("only-user", UserRole.GlobalRoleNames.User));
		await context.SaveChangesAsync();

		var notifier = new ContentModerationNotifier(context);
		await notifier.NotifySuperAdminsAsync("Ops", "Nobody to notify");
		await context.SaveChangesAsync();

		context.Notifications.Should().BeEmpty();
	}

	private static void SeedRoles(ApplicationDbContext context)
	{
		context.UserRoles.AddRange(
			new UserRole
			{
				Id = 1,
				Name = UserRole.GlobalRoleNames.SuperAdmin,
				Scope = RoleScope.Global,
			},
			new UserRole
			{
				Id = 2,
				Name = UserRole.GlobalRoleNames.User,
				Scope = RoleScope.Global,
			},
			new UserRole
			{
				Id = 3,
				Name = UserRole.GlobalRoleNames.Admin,
				Scope = RoleScope.Global,
			});
		context.SaveChanges();
	}

	private static ApplicationUser NewUser(string idPrefix, string globalRoleName)
	{
		var roleId = globalRoleName == UserRole.GlobalRoleNames.SuperAdmin
			? 1
			: globalRoleName == UserRole.GlobalRoleNames.User ? 2 : 3;
		return new ApplicationUser
		{
			Id = $"{idPrefix}-{Guid.NewGuid():N}",
			UserName = $"{idPrefix}@example.com",
			Email = $"{idPrefix}@example.com",
			UserRoleId = roleId,
		};
	}
}
