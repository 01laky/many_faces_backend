/*
 * IAiGrpcService.cs - Interface for AI gRPC client
 *
 * Used by ChatHub to call the Python AI service (many_faces_ai) via gRPC Generate RPC.
 */

using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for calling the AI service (Python) via gRPC.
/// </summary>
public interface IAiGrpcService
{
    /// <summary>
    /// Calls the AI service Generate RPC and returns the generated text (or error message).
    /// </summary>
    /// <param name="prompt">User message / prompt.</param>
    /// <param name="maxNewTokens">Max new tokens to generate (default 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated text, or error message if the RPC failed.</returns>
    Task<string> GenerateAsync(string prompt, int maxNewTokens = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the AI service structured content review RPC and returns a moderation recommendation.
    /// </summary>
    Task<AiContentReviewResult> ReviewContentAsync(
        AiContentReviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiContentReviewRequest(
    ModeratedContentType ContentType,
    int ContentId,
    int ModerationVersion,
    int FaceId,
    string Title,
    string Body,
    string? MediaUrl,
    string CreatorId);

public sealed record AiContentReviewResult(AiReviewRecommendation? Recommendation, string? Error);
