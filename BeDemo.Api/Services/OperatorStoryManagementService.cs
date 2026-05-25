using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Operator story removal: hard-delete with platform DM, or single-image delete without DM.
/// Audit is structured logs only (stories are not <see cref="ModeratedContentType"/>).
/// </summary>
public sealed class OperatorStoryManagementService : IOperatorStoryManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly IWebHostEnvironment _env;
	private readonly ILogger<OperatorStoryManagementService> _logger;

	public OperatorStoryManagementService(
		ApplicationDbContext context,
		IPlatformDirectMessageService platformDm,
		IWebHostEnvironment env,
		ILogger<OperatorStoryManagementService> logger)
	{
		_context = context;
		_platformDm = platformDm;
		_env = env;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task HardDeleteStoryAsync(
		string operatorUserId,
		int storyId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var story = await _context.Stories
			.Include(s => s.StoryFaces)
			.Include(s => s.Images)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		// Idempotent: already gone or not visible on this face (reel/blog parity).
		if (story == null || !StoryVisibility.IsTargetedForFace(story, faceId))
			return;

		var creatorId = story.CreatorId;
		var title = story.Title;

		_logger.LogInformation(
			"Operator {OperatorId} hard-deleting story {StoryId} on face {FaceId}: {Reason}",
			operatorUserId,
			storyId,
			faceId,
			reason.Trim());

		// Best-effort disk cleanup for uploaded slides before EF cascade removes rows.
		foreach (var image in story.Images)
			TryDeleteStoredImageFile(image.ImageUrl);

		_context.Stories.Remove(story);
		await _context.SaveChangesAsync(cancellationToken);

		await SendStoryRemovedDmBestEffortAsync(operatorUserId, creatorId, title, userMessage, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> DeleteStoryImageAsync(
		string operatorUserId,
		int storyId,
		int imageId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var story = await _context.Stories
			.Include(s => s.StoryFaces)
			.Include(s => s.Images)
			.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

		if (story == null || !StoryVisibility.IsTargetedForFace(story, faceId))
			return false;

		var image = story.Images.FirstOrDefault(i => i.Id == imageId);
		if (image == null)
			return false;

		// Block removing the last image while the story is still in the portal live window.
		if (IsStoryLive(story) && story.Images.Count <= 1)
			throw new InvalidOperationException("image_delete_blocked_live");

		_logger.LogInformation(
			"Operator {OperatorId} deleting story image {ImageId} on story {StoryId} face {FaceId}: {Reason}",
			operatorUserId,
			imageId,
			storyId,
			faceId,
			reason.Trim());

		TryDeleteStoredImageFile(image.ImageUrl);
		_context.StoryImages.Remove(image);
		story.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		_ = userMessage;
		return true;
	}

	/// <summary>Portal live window — same predicate as <see cref="StoriesController"/> member reads.</summary>
	internal static bool IsStoryLive(Story story)
	{
		var now = DateTime.UtcNow;
		return story.State == StoryState.Published &&
			   story.PublishedAt.HasValue &&
			   story.PublishedAt <= now &&
			   story.ExpiresAt.HasValue &&
			   story.ExpiresAt > now;
	}

	private async Task SendStoryRemovedDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string storyTitle,
		string userMessage,
		CancellationToken cancellationToken)
	{
		var body =
			$"An administrator removed your story \"{storyTitle}\".\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private static string TruncateUserMessage(string userMessage)
	{
		var trimmed = userMessage.Trim();
		if (trimmed.Length <= PlatformDirectMessageRules.MaxContentLength)
			return trimmed;
		return trimmed[..(PlatformDirectMessageRules.MaxContentLength - 3)] + "...";
	}

	private async Task TrySendDmAsync(
		string operatorUserId,
		string creatorId,
		string content,
		CancellationToken cancellationToken)
	{
		try
		{
			var (errorCode, _) = await _platformDm.SendAsync(operatorUserId, creatorId, content, cancellationToken);
			if (errorCode != null)
			{
				_logger.LogWarning(
					"Platform DM after story action failed: {ErrorCode} operator={OperatorId} creator={CreatorId}",
					errorCode,
					operatorUserId,
					creatorId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"Platform DM after story action threw; operator={OperatorId} creator={CreatorId}",
				operatorUserId,
				creatorId);
		}
	}

	private void TryDeleteStoredImageFile(string storedPath)
	{
		if (string.IsNullOrWhiteSpace(storedPath) ||
			!storedPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
			return;

		var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
			? Path.Combine(_env.ContentRootPath, "wwwroot")
			: _env.WebRootPath;

		var segments = storedPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length < 2)
			return;

		var fileName = segments[^1];
		var dirSegments = segments[..^1];
		if (!UploadPathSecurity.TryResolveFileUnderWebRoot(webRoot, dirSegments, fileName, out var fullPath, out _))
			return;

		try
		{
			if (File.Exists(fullPath))
				File.Delete(fullPath);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to delete story image file at {Path}", fullPath);
		}
	}
}
