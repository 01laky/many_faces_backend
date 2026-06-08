using BeDemo.Api.Services;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Decorator around <see cref="AiGrpcService"/> that no-ops all inference when the global AI switch is off.
/// Enable orchestration resolves <see cref="AiGrpcService"/> directly so health probes are not blocked.
/// </summary>
public sealed class AiAvailabilityGuardGrpcService : IAiGrpcService
{
	private const string DisabledGenerateMessage =
		"AI support is currently disabled for this system. Please contact an operator.";

	private readonly IAiGrpcService _inner;
	private readonly IOperatorAiSystemSettingsProvider _settings;

	public AiAvailabilityGuardGrpcService(
		AiGrpcService inner,
		IOperatorAiSystemSettingsProvider settings)
	{
		_inner = inner;
		_settings = settings;
	}

	/// <inheritdoc />
	public async Task<string> GenerateAsync(
		string prompt,
		int maxNewTokens = 50,
		string? statsContextJson = null,
		string? responseLocale = null,
		CancellationToken cancellationToken = default)
	{
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return DisabledGenerateMessage;

		return await _inner.GenerateAsync(
			prompt,
			maxNewTokens,
			statsContextJson,
			responseLocale,
			cancellationToken);
	}

	/// <inheritdoc />
	public async Task<string> OperatorStatsChatAsync(
		string userMessage,
		string historyText,
		bool fetchLivePublicSnapshot,
		string publicStatsAbsoluteUrl,
		int maxNewTokens = 150,
		CancellationToken cancellationToken = default)
	{
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return DisabledGenerateMessage;

		return await _inner.OperatorStatsChatAsync(
			userMessage,
			historyText,
			fetchLivePublicSnapshot,
			publicStatsAbsoluteUrl,
			maxNewTokens,
			cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AiContentReviewResult> ReviewContentAsync(
		AiContentReviewRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return new AiContentReviewResult(null, "ai_disabled");

		return await _inner.ReviewContentAsync(request, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default)
	{
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return new AiModelStatus(Ready: false, Loading: false, Unavailable: true, ModelName: null);

		return await _inner.GetModelStatusAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default)
	{
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return new AiHostProfileFetchResult(null, "ai_disabled");

		return await _inner.GetHostProfileAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AiEmbedTextResult> EmbedTextAsync(
		string text,
		string? model = null,
		CancellationToken cancellationToken = default)
	{
		// Global AI switch off ⇒ no embeddings. Callers (indexer/retriever) treat the error result as
		// "embed unavailable" — the retriever then takes the planner fallback (§6) and the indexer skips.
		if (!await _settings.IsAiEnabledAsync(cancellationToken))
			return new AiEmbedTextResult(null, null, "ai_disabled");

		return await _inner.EmbedTextAsync(text, model, cancellationToken);
	}
}
