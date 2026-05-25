using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class ChatRoomLifecycleServiceTests
{
	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	private static ChatRoomLifecycleService CreateSut(
		ApplicationDbContext ctx,
		Mock<IRedisJobQueue>? queue = null,
		Mock<IHubContext<ChatRoomHub>>? chatHub = null,
		Mock<IHubContext<MessengerHub>>? messengerHub = null)
	{
		queue ??= new Mock<IRedisJobQueue>();
		chatHub ??= new Mock<IHubContext<ChatRoomHub>>();
		var hubClients = new Mock<IHubClients>();
		var proxy = new Mock<IClientProxy>();
		hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
		chatHub.Setup(h => h.Clients).Returns(hubClients.Object);

		messengerHub ??= new Mock<IHubContext<MessengerHub>>();
		var mClients = new Mock<IHubClients>();
		var mProxy = new Mock<IClientProxy>();
		mClients.Setup(c => c.User(It.IsAny<string>())).Returns(mProxy.Object);
		messengerHub.Setup(h => h.Clients).Returns(mClients.Object);

		return new ChatRoomLifecycleService(
			ctx,
			queue.Object,
			chatHub.Object,
			messengerHub.Object,
			NullLogger<ChatRoomLifecycleService>.Instance);
	}

	[Fact]
	public async Task ProcessIdleCheckAsync_ShouldNoOp_WhenRoomMissing()
	{
		await using var ctx = CreateContext();
		var queue = new Mock<IRedisJobQueue>();
		var sut = CreateSut(ctx, queue);
		await sut.ProcessIdleCheckAsync(999_999);
		queue.Verify(q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task ProcessIdleCheckAsync_ShouldReschedule_WhenLastActivityWithinOneHour()
	{
		await using var ctx = CreateContext();
		var face = new Face
		{
			Index = $"f_{Guid.NewGuid():N}",
			Title = "F",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = false,
			IsPublic = true,
			Visibility = FaceVisibility.Public,
		};
		ctx.Faces.Add(face);
		await ctx.SaveChangesAsync();
		var room = new FaceChatRoom
		{
			FaceId = face.Id,
			Title = "R",
			IsPublic = true,
			IsSystemManaged = false,
			CreatedAt = DateTime.UtcNow,
			LastMessageAt = DateTime.UtcNow.AddMinutes(-30),
		};
		ctx.FaceChatRooms.Add(room);
		await ctx.SaveChangesAsync();

		var queue = new Mock<IRedisJobQueue>();
		queue.Setup(q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		var sut = CreateSut(ctx, queue);
		await sut.ProcessIdleCheckAsync(room.Id);

		queue.Verify(
			q => q.ScheduleAsync("chatroom.idle-check", It.Is<string>(s => s.Contains($"\"faceChatRoomId\":{room.Id}")), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
			Times.Once);
		(await ctx.FaceChatRooms.CountAsync()).Should().Be(1);
	}

	[Fact]
	public async Task ProcessIdleCheckAsync_ShouldDeleteRoom_WhenIdleBeyondOneHour_AndNullCreator()
	{
		await using var ctx = CreateContext();
		var face = new Face
		{
			Index = $"f_{Guid.NewGuid():N}",
			Title = "F",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = false,
			IsPublic = true,
			Visibility = FaceVisibility.Public,
		};
		ctx.Faces.Add(face);
		await ctx.SaveChangesAsync();
		var room = new FaceChatRoom
		{
			FaceId = face.Id,
			Title = "R",
			IsPublic = true,
			IsSystemManaged = false,
			CreatorUserId = null,
			CreatedAt = DateTime.UtcNow.AddHours(-5),
			LastMessageAt = DateTime.UtcNow.AddHours(-2),
		};
		ctx.FaceChatRooms.Add(room);
		await ctx.SaveChangesAsync();
		var roomId = room.Id;

		var queue = new Mock<IRedisJobQueue>();
		var sut = CreateSut(ctx, queue);
		await sut.ProcessIdleCheckAsync(roomId);

		(await ctx.FaceChatRooms.AnyAsync(r => r.Id == roomId)).Should().BeFalse();
		queue.Verify(q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task ScheduleIdleCheckAsync_ShouldEnqueue_WithExpectedType()
	{
		await using var ctx = CreateContext();
		var queue = new Mock<IRedisJobQueue>();
		queue.Setup(q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		var sut = CreateSut(ctx, queue);
		await sut.ScheduleIdleCheckAsync(42);
		queue.Verify(
			q => q.ScheduleAsync("chatroom.idle-check", It.Is<string>(s => s.Contains("\"faceChatRoomId\":42")), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessIdleCheckAsync_ShouldUseCreatedAt_WhenLastMessageAtNull()
	{
		await using var ctx = CreateContext();
		var face = new Face
		{
			Index = $"f_{Guid.NewGuid():N}",
			Title = "F",
			CreatedAt = DateTime.UtcNow,
			AllowRecensions = true,
			ChatRoomsCreate = false,
			IsPublic = true,
			Visibility = FaceVisibility.Public,
		};
		ctx.Faces.Add(face);
		await ctx.SaveChangesAsync();
		var room = new FaceChatRoom
		{
			FaceId = face.Id,
			Title = "R",
			IsPublic = true,
			IsSystemManaged = false,
			CreatorUserId = null,
			CreatedAt = DateTime.UtcNow.AddMinutes(-20),
			LastMessageAt = null,
		};
		ctx.FaceChatRooms.Add(room);
		await ctx.SaveChangesAsync();

		var queue = new Mock<IRedisJobQueue>();
		queue.Setup(q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		var sut = CreateSut(ctx, queue);
		await sut.ProcessIdleCheckAsync(room.Id);

		queue.Verify(
			q => q.ScheduleAsync("chatroom.idle-check", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
