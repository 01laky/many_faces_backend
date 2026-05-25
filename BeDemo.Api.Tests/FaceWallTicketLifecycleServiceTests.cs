using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BeDemo.Api.Tests;

public sealed class FaceWallTicketLifecycleServiceTests
{
	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	[Fact]
	public async Task DeleteTicketHardAsync_ShouldNoOp_WhenTicketMissing()
	{
		await using var db = CreateContext();
		var queue = new Mock<IRedisJobQueue>();
		var sut = new FaceWallTicketLifecycleService(db, queue.Object, NullLogger<FaceWallTicketLifecycleService>.Instance);

		await sut.DeleteTicketHardAsync(404);

		queue.Verify(
			q => q.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task DeleteTicketHardAsync_ShouldRemoveRow_WhenTicketExists()
	{
		await using var db = CreateContext();
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
		db.Faces.Add(face);
		await db.SaveChangesAsync();

		var ticket = new FaceWallTicket
		{
			FaceId = face.Id,
			Title = "T",
			Description = "D",
			Status = FaceWallTicketStatus.Active,
			CreatorUserId = "creator",
			CreatedAt = DateTime.UtcNow,
		};
		db.FaceWallTickets.Add(ticket);
		await db.SaveChangesAsync();

		var sut = new FaceWallTicketLifecycleService(db, Mock.Of<IRedisJobQueue>(), NullLogger<FaceWallTicketLifecycleService>.Instance);
		await sut.DeleteTicketHardAsync(ticket.Id);

		(await db.FaceWallTickets.CountAsync()).Should().Be(0);
	}

	[Fact]
	public async Task ScheduleDeniedTicketDeletionAsync_ShouldEnqueueDelayedJob()
	{
		await using var db = CreateContext();
		var queue = new Mock<IRedisJobQueue>();
		var sut = new FaceWallTicketLifecycleService(db, queue.Object, NullLogger<FaceWallTicketLifecycleService>.Instance);
		var before = DateTime.UtcNow;

		await sut.ScheduleDeniedTicketDeletionAsync(12);

		queue.Verify(
			q => q.ScheduleAsync(
				FaceWallTicketLifecycleService.JobTypeWallTicketDelete,
				"""{"wallTicketId":12}""",
				It.Is<DateTime>(d => d >= before.Add(FaceWallTicketLifecycleService.DeniedRetention).AddSeconds(-2)),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
