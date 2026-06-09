using BeDemo.Api.Models;
using BeDemo.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeDemo.Api.Tests;

public sealed class CapturingOperatorAiGrpcService : IAiGrpcService, IAiModelStatusClient
{
	public Task<AiEmbedTextResult> EmbedTextAsync(string text, string? model = null, CancellationToken cancellationToken = default) =>
		Task.FromResult(new AiEmbedTextResult(null, null, "test fake"));

	public Task<AiGenerateReportResult> GenerateReportAsync(string reportType, string inputJson, int maxNewTokens, CancellationToken cancellationToken = default) =>
		Task.FromResult(new AiGenerateReportResult(null, null, null, "test fake"));

	public string? LastResponseLocale { get; private set; }
	public string? LastPrompt { get; private set; }

	public Func<string?, string>? GenerateHandler { get; set; }

	/// <summary>Override model status returned by enable health probes and model-status API.</summary>
	public Func<AiModelStatus>? ModelStatusHandler { get; set; }

	private int _modelStatusPollCount;

	public int ModelStatusPollCount => _modelStatusPollCount;

	public void ResetModelStatusPollCount() => Interlocked.Exchange(ref _modelStatusPollCount, 0);

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
		var text = GenerateHandler?.Invoke(responseLocale) ?? "...";
		return Task.FromResult(text);
	}

	public async System.Collections.Generic.IAsyncEnumerable<AiGenerateDelta> GenerateStreamAsync(

		string prompt,

		int maxNewTokens = 50,

		string? statsContextJson = null,

		string? responseLocale = null,

		double? temperature = null,

		IReadOnlyList<string>? stopSequences = null,

		string? model = null,

		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)

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
		Task.FromResult(string.Empty);

	public Task<AiContentReviewResult> ReviewContentAsync(
		AiContentReviewRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(new AiContentReviewResult(null, "not used"));

	public Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default)
	{
		_modelStatusPollCount++;
		if (ModelStatusHandler != null)
			return Task.FromResult(ModelStatusHandler());
		return Task.FromResult(new AiModelStatus(true, false, false, "test-model"));
	}

	public Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(new AiHostProfileFetchResult(null, "Unimplemented"));
}

public sealed class OperatorAiGrpcMockWebApplicationFactory : CustomWebApplicationFactory<Program>
{
	public CapturingOperatorAiGrpcService Ai { get; } = new();

	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.UseSetting("OperatorAi:EnableHealthLoadingWaitSeconds", "2");
		builder.UseSetting("OperatorAi:EnableHealthPollIntervalSeconds", "1");
		builder.ConfigureServices(services =>
		{
			services.RemoveAll<IAiModelStatusClient>();
			services.RemoveAll<IAiGrpcService>();
			services.AddSingleton<IAiModelStatusClient>(Ai);
			services.AddSingleton<IAiGrpcService>(Ai);
		});
	}
}
