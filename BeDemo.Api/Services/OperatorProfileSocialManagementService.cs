using BeDemo.Api.Data;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Operator removal of profile comments/reviews. Profiles are not ModeratedContentType;
/// audit is structured logs only (no ContentModerationEvents).
/// </summary>
public sealed class OperatorProfileSocialManagementService : IOperatorProfileSocialManagementService
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformDirectMessageService _platformDm;
	private readonly ILogger<OperatorProfileSocialManagementService> _logger;

	public OperatorProfileSocialManagementService(
		ApplicationDbContext context,
		IPlatformDirectMessageService platformDm,
		ILogger<OperatorProfileSocialManagementService> logger)
	{
		_context = context;
		_platformDm = platformDm;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteCommentAsync(
		string operatorUserId,
		int commentId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var comment = await _context.UserFaceProfileComments
			.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
		if (comment == null)
			return true;

		var profile = await _context.UserFaceProfiles.AsNoTracking()
			.Include(ufp => ufp.UserProfile)
			.FirstOrDefaultAsync(ufp => ufp.Id == comment.UserFaceProfileId, cancellationToken);
		if (profile == null || profile.FaceId != faceId)
			return false;

		var authorId = comment.UserId;
		var displayName = ResolveDisplayName(profile);

		_logger.LogInformation(
			"Operator {OperatorId} deleting profile comment {CommentId} on face {FaceId}: {Reason}",
			operatorUserId,
			commentId,
			faceId,
			reason.Trim());

		_context.UserFaceProfileComments.Remove(comment);
		await _context.SaveChangesAsync(cancellationToken);

		var body =
			$"Your comment on the profile \"{displayName}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, authorId, body, cancellationToken);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteReviewAsync(
		string operatorUserId,
		int reviewId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default)
	{
		var review = await _context.UserFaceProfileReviews
			.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
		if (review == null)
			return true;

		var profile = await _context.UserFaceProfiles.AsNoTracking()
			.Include(ufp => ufp.UserProfile)
			.FirstOrDefaultAsync(ufp => ufp.Id == review.UserFaceProfileId, cancellationToken);
		if (profile == null || profile.FaceId != faceId)
			return false;

		var authorId = review.AuthorUserId;
		var displayName = ResolveDisplayName(profile);

		_logger.LogInformation(
			"Operator {OperatorId} deleting profile review {ReviewId} on face {FaceId}: {Reason}",
			operatorUserId,
			reviewId,
			faceId,
			reason.Trim());

		_context.UserFaceProfileReviews.Remove(review);
		await _context.SaveChangesAsync(cancellationToken);

		var body =
			$"Your review on the profile \"{displayName}\" was removed by platform moderation.\n\n{TruncateUserMessage(userMessage)}";
		await TrySendDmAsync(operatorUserId, authorId, body, cancellationToken);
		return true;
	}

	private static string ResolveDisplayName(Models.UserFaceProfile profile)
	{
		var faceName = profile.DisplayName?.Trim();
		if (!string.IsNullOrEmpty(faceName))
			return faceName;
		return profile.UserProfile?.Nickname?.Trim() ?? "Profile";
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
		string recipientId,
		string content,
		CancellationToken cancellationToken)
	{
		try
		{
			var (errorCode, _) = await _platformDm.SendAsync(operatorUserId, recipientId, content, cancellationToken);
			if (errorCode != null)
			{
				_logger.LogWarning(
					"Platform DM after profile UGC delete failed: {ErrorCode} operator={OperatorId} recipient={RecipientId}",
					errorCode,
					operatorUserId,
					recipientId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"Platform DM after profile UGC delete threw; operator={OperatorId} recipient={RecipientId}",
				operatorUserId,
				recipientId);
		}
	}
}
