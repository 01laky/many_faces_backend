using System.Runtime.CompilerServices;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests.TestDoubles;

/// <summary>
/// Canonical configurable test double for <see cref="IAiGrpcService"/> (backend-refactor Phase 0 — consolidation of
/// the per-file hand-rolled fakes). Every RPC has a sensible no-op default and a single, obvious configuration knob,
/// and the call inputs that tests assert on are captured. It also implements <see cref="IAiModelStatusClient"/> (the
/// unguarded health-probe surface) so it can stand in for both registrations.
/// <para>
/// Defaults: <see cref="GenerateAsync"/> → empty string; <see cref="ReviewContentAsync"/> → a low-risk "approve"
/// recommendation; model status → ready; host profile / embed / report → an "unavailable" stub. Override any of them
/// via the settable properties or the convenience constructors.
/// </para>
/// </summary>
public sealed class FakeAiGrpcService : IAiGrpcService, IAiModelStatusClient
{
	/// <summary>A neutral low-risk "approve" recommendation used as the default review result.</summary>
	public static AiReviewRecommendation DefaultApprove { get; } = new(
		AiReviewDecision.Approve,
		0.92,
		AiReviewRiskLevel.Low,
		Array.Empty<string>(),
		"ok",
		"msg",
		"m",
		"t");

	// ---- ReviewContent --------------------------------------------------------------------------------------------

	/// <summary>The result returned by <see cref="ReviewContentAsync"/>. Defaults to <see cref="DefaultApprove"/>.</summary>
	public AiContentReviewResult ReviewResult { get; set; } = new(DefaultApprove, null);

	/// <summary>The last request passed to <see cref="ReviewContentAsync"/> (null until first call).</summary>
	public AiContentReviewRequest? LastReviewRequest { get; private set; }

	// ---- Generate -------------------------------------------------------------------------------------------------

	/// <summary>Maps (prompt, responseLocale) → generated text. Defaults to empty string.</summary>
	public Func<string, string?, string> GenerateHandler { get; set; } = static (_, _) => string.Empty;

	public string? LastPrompt { get; private set; }
	public string? LastResponseLocale { get; private set; }

	// ---- ModelStatus ----------------------------------------------------------------------------------------------

	/// <summary>Returns the model status. Defaults to "ready".</summary>
	public Func<AiModelStatus> ModelStatusHandler { get; set; } = static () => new AiModelStatus(true, false, false, "test-model");

	private int _modelStatusPollCount;
	public int ModelStatusPollCount => _modelStatusPollCount;
	public void ResetModelStatusPollCount() => Interlocked.Exchange(ref _modelStatusPollCount, 0);

	// ---- Other RPCs (simple settable stubs) -----------------------------------------------------------------------

	public AiHostProfileFetchResult HostProfileResult { get; set; } = new(null, "Unimplemented");
	public AiEmbedTextResult EmbedResult { get; set; } = new(null, null, "test fake");
	public AiGenerateReportResult ReportResult { get; set; } = new(null, null, null, "test fake");
	public Func<string, string, bool, string, string> OperatorStatsChatHandler { get; set; } = static (_, _, _, _) => string.Empty;

	// ---- Constructors ---------------------------------------------------------------------------------------------

	/// <summary>Default fake: review returns <see cref="DefaultApprove"/>.</summary>
	public FakeAiGrpcService() { }

	/// <summary>Review returns the given recommendation (no error).</summary>
	public FakeAiGrpcService(AiReviewRecommendation recommendation) => ReviewResult = new AiContentReviewResult(recommendation, null);

	/// <summary>Review returns the given error (no recommendation).</summary>
	public FakeAiGrpcService(string error) => ReviewResult = new AiContentReviewResult(null, error);

	// ---- IAiGrpcService -------------------------------------------------------------------------------------------

	public Task<string> GenerateAsync(
		string prompt,
		int maxNewTokens = 50,
		string? statsContextJson = null,
		string? responseLocale = null,
		double? temperature = null,
		IReadOnlyList<string>? stopSequences = null,
		string? model = null,
		CancellationToken cancellationToken = default)
	{
		LastPrompt = prompt;
		LastResponseLocale = responseLocale;
		return Task.FromResult(GenerateHandler(prompt, responseLocale));
	}

	public async IAsyncEnumerable<AiGenerateDelta> GenerateStreamAsync(
		string prompt,
		int maxNewTokens = 50,
		string? statsContextJson = null,
		string? responseLocale = null,
		double? temperature = null,
		IReadOnlyList<string>? stopSequences = null,
		string? model = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var text = await GenerateAsync(prompt, maxNewTokens, statsContextJson, responseLocale, temperature, stopSequences, model, cancellationToken);
		yield return new AiGenerateDelta(text, true, "stop", null, null);
	}

	public Task<string> OperatorStatsChatAsync(
		string userMessage,
		string historyText,
		bool fetchLivePublicSnapshot,
		string publicStatsAbsoluteUrl,
		int maxNewTokens = 150,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(OperatorStatsChatHandler(userMessage, historyText, fetchLivePublicSnapshot, publicStatsAbsoluteUrl));

	public Task<AiContentReviewResult> ReviewContentAsync(AiContentReviewRequest request, CancellationToken cancellationToken = default)
	{
		LastReviewRequest = request;
		return Task.FromResult(ReviewResult);
	}

	public Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref _modelStatusPollCount);
		return Task.FromResult(ModelStatusHandler());
	}

	public Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(HostProfileResult);

	public Task<AiEmbedTextResult> EmbedTextAsync(string text, string? model = null, CancellationToken cancellationToken = default) =>
		Task.FromResult(EmbedResult);

	public Task<AiGenerateReportResult> GenerateReportAsync(string reportType, string inputJson, int maxNewTokens, CancellationToken cancellationToken = default) =>
		Task.FromResult(ReportResult);
}
