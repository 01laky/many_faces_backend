using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Hard-deletes blogs (operator Remove = Delete blog), removes single images, and sends creator DMs without rolling back on messenger failure.
/// </summary>
public sealed class OperatorBlogManagementService : IOperatorBlogManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly ILogger<OperatorBlogManagementService> _logger;

	public OperatorBlogManagementService(
		ApplicationDbContext context,
		IPlatformDirectMessageService platformDm,
		ILogger<OperatorBlogManagementService> logger)
	{
		_context = context;
		_platformDm = platformDm;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> HardDeleteBlogAsync(
		string operatorUserId,
		int blogId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var blog = await _context.Blogs
			.Include(b => b.Images)
			.FirstOrDefaultAsync(b => b.Id == blogId, cancellationToken);

		// Idempotent: blog already gone or wrong face scope → 204.
		if (blog == null || blog.FaceId != faceId)
			return true;

		var creatorId = blog.CreatorId;
		var title = blog.Title;

		// Audit while the row still exists (hard delete, not Removed status).
		_context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
			ModeratedContentType.Blog,
			blog.Id,
			blog.FaceId,
			blog.ApprovalStatus,
			blog.ApprovalStatus,
			blog.AiReviewStatus,
			blog.AiReviewStatus,
			ModerationActorType.SuperAdmin,
			operatorUserId,
			reason.Trim(),
			userMessage.Trim(),
			blog.AiReviewTraceId,
			blog.AiReviewModelVersion));

		_context.Blogs.Remove(blog);
		await _context.SaveChangesAsync(cancellationToken);

		await SendBlogRemovedDmBestEffortAsync(operatorUserId, creatorId, title, userMessage, cancellationToken);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteBlogImageAsync(
		string operatorUserId,
		int blogId,
		int imageId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var blog = await _context.Blogs
			.Include(b => b.Images)
			.FirstOrDefaultAsync(b => b.Id == blogId, cancellationToken);

		if (blog == null || blog.FaceId != faceId)
			return false;

		var image = blog.Images.FirstOrDefault(i => i.Id == imageId);
		if (image == null)
			return false;

		var imageLabel = ResolveImageLabel(image);
		var blogTitle = blog.Title;

		_context.BlogImages.Remove(image);
		await _context.SaveChangesAsync(cancellationToken);

		var body = BuildImageRemovedMessage(imageLabel, blogTitle, userMessage);
		await TrySendDmAsync(operatorUserId, blog.CreatorId, body, cancellationToken);
		_ = reason;
		return true;
	}

	/// <inheritdoc />
	public Task SendRejectDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string blogTitle,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var body =
			$"Your blog \"{blogTitle}\" was rejected by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		return TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private async Task SendBlogRemovedDmBestEffortAsync(
		string operatorUserId,
		string creatorId,
		string blogTitle,
		string userMessage,
		CancellationToken cancellationToken)
	{
		var body =
			$"Your blog \"{blogTitle}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, creatorId, body, cancellationToken);
	}

	private static string BuildImageRemovedMessage(string imageLabel, string blogTitle, string userMessage) =>
		$"An image ({imageLabel}) was removed from your blog \"{blogTitle}\".\n\n{TruncateUserMessage(userMessage)}";

	private static string ResolveImageLabel(BlogImage image) => $"Photo #{image.SortOrder + 1}";

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
					"Platform DM after blog action failed: {ErrorCode} operator={OperatorId} creator={CreatorId}",
					errorCode,
					operatorUserId,
					creatorId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"Platform DM after blog action threw; operator={OperatorId} creator={CreatorId}",
				operatorUserId,
				creatorId);
		}
	}
}
