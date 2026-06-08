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
	/// <param name="statsContextJson">When set, forwarded to the Python worker as read-only aggregate context (admin AI).</param>
	Task<string> GenerateAsync(
		string prompt,
		int maxNewTokens = 50,
		string? statsContextJson = null,
		string? responseLocale = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Optional path: Python may HTTP-fetch public aggregate JSON then answer with the local model.
	/// </summary>
	Task<string> OperatorStatsChatAsync(
		string userMessage,
		string historyText,
		bool fetchLivePublicSnapshot,
		string publicStatsAbsoluteUrl,
		int maxNewTokens = 150,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Calls the AI service structured content review RPC and returns a moderation recommendation.
	/// </summary>
	Task<AiContentReviewResult> ReviewContentAsync(
		AiContentReviewRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Lightweight readiness probe (gRPC HealthCheck JSON in <c>message</c>).
	/// </summary>
	Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Fetches host hardware/OS JSON from the Python worker (GetHostProfile RPC).
	/// </summary>
	Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Embeds a single text via the AI worker <c>EmbedText</c> RPC (AI-UP15) and returns the dense vector.
	/// Used by the RAG retrieval refactor (v1): index plane embeds bundle descriptors, query plane embeds
	/// the operator question. The backend supplies the model name from <c>AiService:EmbeddingModel</c> so the
	/// worker, ES mapping and config never drift (§5.5/§5.6).
	/// </summary>
	/// <returns>A result carrying the float vector + the worker's model version, or an error message.</returns>
	Task<AiEmbedTextResult> EmbedTextAsync(
		string text,
		string? model = null,
		CancellationToken cancellationToken = default);
}

/// <summary>Local Qwen model readiness reported by many_faces_ai HealthCheck.</summary>
public sealed record AiModelStatus(bool Ready, bool Loading, bool Unavailable, string? ModelName);

/// <summary>Raw host profile payload from GetHostProfile.</summary>
public sealed record AiHostProfileFetchResult(string? JsonBody, string? Error);

/// <summary>
/// Result of an <c>EmbedText</c> call. <see cref="Vector"/> is null when the worker returned an error or no vector
/// (callers treat that as "embed unavailable" and fall back to the legacy planner, §6/§17.7).
/// </summary>
public sealed record AiEmbedTextResult(float[]? Vector, string? ModelVersion, string? Error)
{
	/// <summary>True when a usable, non-empty vector was returned.</summary>
	public bool HasVector => Vector is { Length: > 0 };
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
