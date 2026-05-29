using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests.Messenger;

/// <summary>MO-SR43-BE2 — DM cursor pagination query contract (in-memory).</summary>
public sealed class MessagesPaginationQueryTests
{
	[Fact]
	public async Task MO_SR43_BE2_beforeId_pages_are_disjoint_and_older()
	{
		var dbName = Guid.NewGuid().ToString();
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;

		await using var db = new ApplicationDbContext(options);
		const string userA = "user-a";
		const string userB = "user-b";

		for (var i = 1; i <= 60; i++)
		{
			db.Messages.Add(new Message
			{
				Id = i,
				SenderId = i % 2 == 0 ? userA : userB,
				ReceiverId = i % 2 == 0 ? userB : userA,
				Content = $"msg-{i}",
				SentAt = DateTime.UtcNow.AddMinutes(i),
				IsMessageRequest = false,
			});
		}
		await db.SaveChangesAsync();

		const int limit = 50;
		var page1 = await db.Messages
			.Where(m =>
				((m.SenderId == userA && m.ReceiverId == userB) ||
				 (m.SenderId == userB && m.ReceiverId == userA)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted))
			.OrderByDescending(m => m.Id)
			.Take(limit)
			.ToListAsync();
		page1.Reverse();
		var oldestId = page1[0].Id;

		var page2 = await db.Messages
			.Where(m =>
				((m.SenderId == userA && m.ReceiverId == userB) ||
				 (m.SenderId == userB && m.ReceiverId == userA)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted) &&
				m.Id < oldestId)
			.OrderByDescending(m => m.Id)
			.Take(limit)
			.ToListAsync();
		page2.Reverse();

		Assert.NotEmpty(page1);
		Assert.NotEmpty(page2);
		Assert.All(page2, m => Assert.True(m.Id < oldestId));
		Assert.Empty(page1.Select(p => p.Id).Intersect(page2.Select(p => p.Id)));
	}

	[Fact]
	public async Task MO_SR43_BE2_empty_page_when_beforeId_is_oldest()
	{
		var dbName = Guid.NewGuid().ToString();
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;

		await using var db = new ApplicationDbContext(options);
		const string userA = "user-a";
		const string userB = "user-b";

		db.Messages.Add(new Message
		{
			Id = 1,
			SenderId = userA,
			ReceiverId = userB,
			Content = "only",
			SentAt = DateTime.UtcNow,
			IsMessageRequest = false,
		});
		await db.SaveChangesAsync();

		var page = await db.Messages
			.Where(m =>
				((m.SenderId == userA && m.ReceiverId == userB) ||
				 (m.SenderId == userB && m.ReceiverId == userA)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted) &&
				m.Id < 1)
			.OrderByDescending(m => m.Id)
			.Take(50)
			.ToListAsync();

		Assert.Empty(page);
	}

	[Fact]
	public async Task MO_SR43_BE2_excludes_pending_message_requests_from_pagination()
	{
		var dbName = Guid.NewGuid().ToString();
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;

		await using var db = new ApplicationDbContext(options);
		const string userA = "user-a";
		const string userB = "user-b";

		db.Messages.AddRange(
			new Message
			{
				Id = 1,
				SenderId = userA,
				ReceiverId = userB,
				Content = "accepted",
				SentAt = DateTime.UtcNow,
				IsMessageRequest = false,
			},
			new Message
			{
				Id = 2,
				SenderId = userB,
				ReceiverId = userA,
				Content = "pending-request",
				SentAt = DateTime.UtcNow.AddMinutes(1),
				IsMessageRequest = true,
				MessageRequestStatus = MessageRequestStatus.Pending,
			});
		await db.SaveChangesAsync();

		var rows = await db.Messages
			.Where(m =>
				((m.SenderId == userA && m.ReceiverId == userB) ||
				 (m.SenderId == userB && m.ReceiverId == userA)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted))
			.OrderByDescending(m => m.Id)
			.ToListAsync();

		Assert.Single(rows);
		Assert.Equal(1, rows[0].Id);
	}
}
