using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Startup assertion (§5.5, RT-2 backend side). Embeds a fixed probe string once via the AI worker EmbedText RPC
/// and asserts the returned vector length == <see cref="AiServiceOptions.EmbeddingDim"/>. On mismatch it logs
/// loud (critical) — backend, worker and the ES mapping must never silently drift.
///
/// <para>Why a hosted service (not a hard crash):</para>
/// The probe needs the AI worker reachable, which may lag backend boot. We log a critical error on drift rather
/// than throwing from <c>Program.cs</c> (which would crash the API even when the operator-AI feature is unused).
/// The Go worker independently rejects dim-mismatched documents, so a wrong dim can never corrupt the index even
/// if this probe is skipped. Set <c>AiService:AssertEmbeddingDimOnStartup=false</c> to disable.
///
/// <para>Inputs/outputs:</para>
/// Reads <c>AiService:EmbeddingModel</c>/<c>EmbeddingDim</c>; calls EmbedText("probe"); emits a single log line.
/// Non-blocking — runs on a background task after a short delay.
/// </summary>
public sealed class OperatorAiEmbeddingDimStartupAssertion : BackgroundService
{
	private const string ProbeText = "many faces operator ai embedding dimension probe";

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly AiServiceOptions _options;
	private readonly OperatorAiEmbeddingDimStatus _dimStatus;
	private readonly ILogger<OperatorAiEmbeddingDimStartupAssertion> _logger;

	public OperatorAiEmbeddingDimStartupAssertion(
		IServiceScopeFactory scopeFactory,
		IOptions<AiServiceOptions> options,
		OperatorAiEmbeddingDimStatus dimStatus,
		ILogger<OperatorAiEmbeddingDimStartupAssertion> logger)
	{
		_scopeFactory = scopeFactory;
		_options = options.Value;
		_dimStatus = dimStatus;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.AssertEmbeddingDimOnStartup)
			return;

		try
		{
			await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var ai = scope.ServiceProvider.GetRequiredService<IAiGrpcService>();
			var settings = scope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();

			// Only meaningful when AI is enabled (otherwise EmbedText is no-op'd by the availability guard).
			if (!await settings.IsAiEnabledAsync(stoppingToken))
			{
				_logger.LogInformation("Embedding dim assertion skipped: global AI disabled.");
				return;
			}

			var result = await ai.EmbedTextAsync(ProbeText, _options.EmbeddingModel, stoppingToken);
			if (!result.HasVector)
			{
				_logger.LogWarning(
					"Embedding dim assertion could not run: EmbedText returned no vector ({Error}). Worker may still be loading.",
					result.Error);
				return;
			}

			var actual = result.Vector!.Length;
			if (actual != _options.EmbeddingDim)
			{
				_logger.LogCritical(
					"EMBEDDING DIM MISMATCH: AiService:EmbeddingDim={Expected} but model '{Model}' (worker reported '{WorkerModel}') returned {Actual}-dim vectors. " +
					"RAG indexing will be rejected by the worker. Fix AiService:EmbeddingDim/EmbeddingModel to match the deployed embed model.",
					_options.EmbeddingDim,
					_options.EmbeddingModel,
					result.ModelVersion,
					actual);
				_dimStatus.Record(ok: false, actual: actual);
				return;
			}

			_dimStatus.Record(ok: true, actual: actual);
			_logger.LogInformation(
				"Embedding dim assertion OK: model '{Model}' returned {Dim}-dim vectors (== configured EmbeddingDim).",
				_options.EmbeddingModel,
				actual);
		}
		catch (OperationCanceledException)
		{
			// Host shutting down.
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Embedding dim assertion failed to run.");
		}
	}
}
