using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Edge cases for platform DM persistence and validation (§10.1).</summary>
public sealed class PlatformDirectMessageServiceTests
{
	[Fact]
	public async Task SendAsync_ShouldRejectEmptyContent()
	{
		var (svc, _) = CreateService();
		var (code, id) = await svc.SendAsync("s1", "r1", "   ");
		code.Should().Be(OperatorUserChatHubErrorCodes.EmptyContent);
		id.Should().BeNull();
	}

	[Fact]
	public async Task SendAsync_ShouldRejectMessageTooLong()
	{
		var (svc, _) = CreateService();
		var content = new string('x', PlatformDirectMessageRules.MaxContentLength + 1);
		var (code, _) = await svc.SendAsync("s1", "r1", content);
		code.Should().Be(OperatorUserChatHubErrorCodes.MessageTooLong);
	}

	[Fact]
	public async Task SendAsync_ShouldRejectSelfSend()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		var (code, _) = await svc.SendAsync("s1", "s1", "hi");
		code.Should().Be(OperatorUserChatHubErrorCodes.CannotMessageSelf);
	}

	[Fact]
	public async Task UserInitiatedSend_WithoutPlatformThread_ShouldReturnNoPlatformThread()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		var (code, _) = await svc.SendAsync("u1", "s1", "hi");
		code.Should().Be(OperatorUserChatHubErrorCodes.NoPlatformThread);
	}

	[Fact]
	public async Task SendAsync_ShouldRejectNonSuperAdminToRegularUser()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		var userRole = await db.UserRoles.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.User);
		db.Users.Add(new ApplicationUser
		{
			Id = "u2",
			UserName = "u2@test",
			Email = "u2@test",
			UserRoleId = userRole.Id,
		});
		await db.SaveChangesAsync();
		var (code, _) = await svc.SendAsync("u1", "u2", "hi");
		code.Should().Be(OperatorUserChatHubErrorCodes.NotSuperAdmin);
	}

	[Fact]
	public async Task SendAsync_ShouldRejectSuperAdminToSuperAdmin()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		await AddSuperAdminAsync(db, "s2");
		var (code, _) = await svc.SendAsync("s1", "s2", "hi");
		code.Should().Be(OperatorUserChatHubErrorCodes.CannotMessageSuperAdmin);
	}

	[Fact]
	public async Task SendAsync_ShouldPersistPlatformFlag_AndNotMessageRequest()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		var (code, id) = await svc.SendAsync("s1", "u1", "hello platform");
		code.Should().BeNull();
		id.Should().NotBeNull();
		var row = await db.Messages.SingleAsync(m => m.Id == id);
		row.IsPlatformDirectMessage.Should().BeTrue();
		row.IsMessageRequest.Should().BeFalse();
	}

	[Fact]
	public async Task UserReply_AfterSuperAdminInitiatedThread_ShouldPersistPlatformFlag()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		(await svc.SendAsync("s1", "u1", "hello platform")).HubErrorCode.Should().BeNull();
		var (code, id) = await svc.SendAsync("u1", "s1", "reply from user");
		code.Should().BeNull();
		id.Should().NotBeNull();
		var row = await db.Messages.SingleAsync(m => m.Id == id);
		row.IsPlatformDirectMessage.Should().BeTrue();
		row.IsMessageRequest.Should().BeFalse();
	}

	[Fact]
	public async Task UserReply_WithoutPlatformThread_ShouldReturnNoPlatformThread()
	{
		var (svc, db) = CreateService();
		await SeedUsersAsync(db, superId: "s1", userId: "u1");
		var (code, _) = await svc.SendAsync("u1", "s1", "reply without seed");
		code.Should().Be(OperatorUserChatHubErrorCodes.NoPlatformThread);
	}

	private static (PlatformDirectMessageService Svc, ApplicationDbContext Db) CreateService()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"platform-dm-{Guid.NewGuid():N}")
			.Options;
		var db = new ApplicationDbContext(options);
		var hub = new Mock<IHubContext<MessengerHub>>();
		var clients = new Mock<IHubClients>();
		var userClients = new Mock<IClientProxy>();
		hub.Setup(h => h.Clients).Returns(clients.Object);
		clients.Setup(c => c.User(It.IsAny<string>())).Returns(userClients.Object);
		userClients
			.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var svc = new PlatformDirectMessageService(db, hub.Object, NullLogger<PlatformDirectMessageService>.Instance);
		return (svc, db);
	}

	private static async Task SeedUsersAsync(ApplicationDbContext db, string superId, string userId)
	{
		var superRole = new UserRole { Id = 1, Name = UserRole.GlobalRoleNames.SuperAdmin };
		var userRole = new UserRole { Id = 2, Name = UserRole.GlobalRoleNames.User };
		db.UserRoles.AddRange(superRole, userRole);
		db.Users.Add(new ApplicationUser
		{
			Id = superId,
			UserName = "super@test",
			Email = "super@test",
			UserRoleId = superRole.Id,
		});
		db.Users.Add(new ApplicationUser
		{
			Id = userId,
			UserName = "user@test",
			Email = "user@test",
			UserRoleId = userRole.Id,
		});
		await db.SaveChangesAsync();
	}

	private static async Task AddSuperAdminAsync(ApplicationDbContext db, string id)
	{
		var role = await db.UserRoles.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
		db.Users.Add(new ApplicationUser
		{
			Id = id,
			UserName = $"{id}@test",
			Email = $"{id}@test",
			UserRoleId = role.Id,
		});
		await db.SaveChangesAsync();
	}
}
