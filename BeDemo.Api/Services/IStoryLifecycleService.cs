namespace BeDemo.Api.Services;

public interface IStoryLifecycleService
{
	/// <summary>Before creating a new draft: remove oldest stories until this creator has at most two rows.</summary>
	Task EnsureRoomForNewStoryAsync(string creatorId, CancellationToken cancellationToken = default);

	/// <summary>Publish now, schedule for later, or re-publish an <see cref="StoryState.Expired"/> story.</summary>
	Task<(bool Ok, string? Error)> TryPublishAsync(
		string creatorId,
		int storyId,
		DateTime? scheduledPublishAtUtc,
		CancellationToken cancellationToken = default);

	/// <summary>From Redis when <see cref="StoryState.Scheduled"/> publish time is due.</summary>
	Task ApplyScheduledPublishAsync(int storyId, CancellationToken cancellationToken = default);

	/// <summary>From Redis when story should expire (24h after publish).</summary>
	Task ApplyExpireAsync(int storyId, CancellationToken cancellationToken = default);

	/// <summary>Deletes oldest stories until the creator has at most three rows.</summary>
	Task EnforceMaxThreeStoriesPerCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
}
