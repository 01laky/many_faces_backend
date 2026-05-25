namespace BeDemo.Api.Services;

/// <summary>Super-admin delete of face profile UGC (comments/reviews) with optional author platform DM.</summary>
public interface IOperatorProfileSocialManagementService
{
	Task<bool> DeleteCommentAsync(
		string operatorUserId,
		int commentId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);

	Task<bool> DeleteReviewAsync(
		string operatorUserId,
		int reviewId,
		int faceId,
		string reason,
		string userMessage,
		CancellationToken cancellationToken = default);
}
