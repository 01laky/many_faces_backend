using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Utils;

public static class StoryInteractionGuard
{
	/// <summary>Likes, comments: live story, face targeting, viewer non-host in that face.</summary>
	public static async Task<Story?> GetLiveStoryForViewerAsync(
		ApplicationDbContext context,
		int storyId,
		int faceId,
		string viewerUserId,
		CancellationToken cancellationToken = default)
	{
		if (!await StoryViewerRules.ViewerIsActiveNonHostInFaceAsync(context, viewerUserId, faceId, cancellationToken))
			return null;

		var story = await context.Stories
			.Include(s => s.StoryFaces)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		if (story == null)
			return null;

		var now = DateTime.UtcNow;
		if (story.State != StoryState.Published ||
			story.PublishedAt == null || story.PublishedAt > now ||
			story.ExpiresAt == null || story.ExpiresAt <= now)
			return null;

		if (!StoryVisibility.IsTargetedForFace(story, faceId))
			return null;

		return story;
	}
}
