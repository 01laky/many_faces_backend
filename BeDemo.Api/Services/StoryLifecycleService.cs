using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

public sealed class StoryLifecycleService : IStoryLifecycleService
{
	private readonly ApplicationDbContext _context;
	private readonly IRedisJobQueue _jobQueue;
	private readonly ILogger<StoryLifecycleService> _logger;

	public StoryLifecycleService(
		ApplicationDbContext context,
		IRedisJobQueue jobQueue,
		ILogger<StoryLifecycleService> logger)
	{
		_context = context;
		_jobQueue = jobQueue;
		_logger = logger;
	}

	public async Task EnsureRoomForNewStoryAsync(string creatorId, CancellationToken cancellationToken = default)
	{
		while (true)
		{
			var count = await _context.Stories.CountAsync(s => s.CreatorId == creatorId, cancellationToken);
			if (count < 3)
				return;

			var oldest = await _context.Stories
				.Where(s => s.CreatorId == creatorId)
				.OrderBy(s => s.CreatedAt)
				.FirstAsync(cancellationToken);
			_context.Stories.Remove(oldest);
			await _context.SaveChangesAsync(cancellationToken);
		}
	}

	public async Task<(bool Ok, string? Error)> TryPublishAsync(
		string creatorId,
		int storyId,
		DateTime? scheduledPublishAtUtc,
		CancellationToken cancellationToken = default)
	{
		var story = await _context.Stories
			.Include(s => s.Images)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		if (story == null)
			return (false, "not_found");
		if (story.CreatorId != creatorId)
			return (false, "forbidden");

		if (story.Images.Count < 1 || story.Images.Count > 10)
			return (false, "invalid_images");

		var now = DateTime.UtcNow;
		if (story.State == StoryState.Published && story.ExpiresAt.HasValue && story.ExpiresAt.Value > now)
			return (false, "already_published");

		if (story.State == StoryState.Expired)
		{
			story.PublishedAt = null;
			story.ExpiresAt = null;
		}

		if (scheduledPublishAtUtc.HasValue && scheduledPublishAtUtc.Value > now)
		{
			story.State = StoryState.Scheduled;
			story.ScheduledPublishAt = scheduledPublishAtUtc.Value;
			story.UpdatedAt = now;
			await _context.SaveChangesAsync(cancellationToken);
			try
			{
				await _jobQueue.ScheduleAsync(
					"story.publish",
					JsonSerializer.Serialize(new { storyId }),
					scheduledPublishAtUtc.Value,
					cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to schedule story.publish for {StoryId}", storyId);
			}

			await EnforceMaxThreeStoriesPerCreatorAsync(story.CreatorId, cancellationToken);
			return (true, null);
		}

		var publishedAt = now;
		story.PublishedAt = publishedAt;
		story.ExpiresAt = publishedAt.AddHours(24);
		story.State = StoryState.Published;
		story.ScheduledPublishAt = null;
		story.UpdatedAt = now;
		await _context.SaveChangesAsync(cancellationToken);
		await ScheduleExpireJobInternalAsync(storyId, story.ExpiresAt.Value, cancellationToken);
		await EnforceMaxThreeStoriesPerCreatorAsync(story.CreatorId, cancellationToken);
		return (true, null);
	}

	public async Task ApplyScheduledPublishAsync(int storyId, CancellationToken cancellationToken = default)
	{
		var story = await _context.Stories
			.Include(s => s.Images)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		if (story == null || story.State != StoryState.Scheduled)
			return;

		var now = DateTime.UtcNow;
		if (story.ScheduledPublishAt.HasValue && story.ScheduledPublishAt.Value > now.AddSeconds(5))
		{
			_logger.LogInformation("Story {StoryId} publish job too early; skipping", storyId);
			return;
		}

		if (story.Images.Count == 0 || story.Images.Count > 10)
		{
			_logger.LogWarning("Story {StoryId} cannot publish: invalid image count {Count}", storyId, story.Images.Count);
			return;
		}

		var publishedAt = story.ScheduledPublishAt ?? now;
		story.PublishedAt = publishedAt;
		story.ExpiresAt = publishedAt.AddHours(24);
		story.State = StoryState.Published;
		story.ScheduledPublishAt = null;
		story.UpdatedAt = now;

		await _context.SaveChangesAsync(cancellationToken);
		await ScheduleExpireJobInternalAsync(storyId, story.ExpiresAt.Value, cancellationToken);
		await EnforceMaxThreeStoriesPerCreatorAsync(story.CreatorId, cancellationToken);
		_logger.LogInformation("Story {StoryId} published (scheduled path)", storyId);
	}

	public async Task ApplyExpireAsync(int storyId, CancellationToken cancellationToken = default)
	{
		var story = await _context.Stories
			.Include(s => s.Likes)
			.Include(s => s.Comments)
			.Include(s => s.Views)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		if (story == null || story.State != StoryState.Published)
			return;

		var now = DateTime.UtcNow;
		if (story.ExpiresAt.HasValue && story.ExpiresAt.Value > now.AddMinutes(1))
		{
			_logger.LogInformation("Story {StoryId} expire job early; ignoring", storyId);
			return;
		}

		_context.StoryLikes.RemoveRange(story.Likes);
		_context.StoryComments.RemoveRange(story.Comments);
		_context.StoryViews.RemoveRange(story.Views);

		story.State = StoryState.Expired;
		story.UpdatedAt = now;

		await _context.SaveChangesAsync(cancellationToken);
		await EnforceMaxThreeStoriesPerCreatorAsync(story.CreatorId, cancellationToken);
		_logger.LogInformation("Story {StoryId} expired; interactions cleared", storyId);
	}

	public async Task EnforceMaxThreeStoriesPerCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
	{
		var stories = await _context.Stories
			.Where(s => s.CreatorId == creatorId)
			.OrderBy(s => s.CreatedAt)
			.ToListAsync(cancellationToken);

		while (stories.Count > 3)
		{
			var oldest = stories[0];
			_context.Stories.Remove(oldest);
			stories.RemoveAt(0);
		}

		if (_context.ChangeTracker.HasChanges())
			await _context.SaveChangesAsync(cancellationToken);
	}

	private async Task ScheduleExpireJobInternalAsync(int storyId, DateTime expiresAtUtc, CancellationToken cancellationToken)
	{
		try
		{
			await _jobQueue.ScheduleAsync(
				"story.expire",
				JsonSerializer.Serialize(new { storyId }),
				expiresAtUtc,
				cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to schedule story.expire for {StoryId}", storyId);
		}
	}
}
