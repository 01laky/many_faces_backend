using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi.Skills;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// 7B-perf O8 + O10 — startup warm. Non-blocking BackgroundService that, when AI is enabled and reachable:
/// (O8) warms the 4 skill routing descriptor vectors so the first operator turn doesn't pay the descriptor embed, and
/// (O10) issues one tiny throwaway <c>Generate</c> (<c>num_predict=1</c>) to force the model fully loaded and its GPU
/// layers allocated, so the first real message isn't a cold load. Both are best-effort + time-boxed and never block
/// boot; failure just means the first turn pays the warm as before.
/// </summary>
public sealed class OperatorAiStartupWarmService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IHostEnvironment _environment;
	private readonly IOptions<AiServiceOptions> _aiOptions;
	private readonly ILogger<OperatorAiStartupWarmService> _logger;

	public OperatorAiStartupWarmService(
		IServiceScopeFactory scopeFactory,
		IHostEnvironment environment,
		IOptions<AiServiceOptions> aiOptions,
		ILogger<OperatorAiStartupWarmService> logger)
	{
		_scopeFactory = scopeFactory;
		_environment = environment;
		_aiOptions = aiOptions;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (_environment.IsEnvironment("Testing"))
			return;

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var systemSettings = scope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();
			if (!await systemSettings.IsAiEnabledAsync(stoppingToken))
				return;

			var timeoutSeconds = Math.Max(5, _aiOptions.Value.WarmUpStartupTimeoutSeconds);
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

			// O8 — warm the descriptor vectors (4 embeds, once).
			try
			{
				var router = scope.ServiceProvider.GetRequiredService<IOperatorAiSkillRouter>();
				var warmed = await router.WarmAsync(timeoutCts.Token);
				_logger.LogInformation("Operator AI skill-vector startup warm: warmed={Warmed}.", warmed);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogWarning(ex, "Operator AI skill-vector startup warm failed (non-fatal).");
			}

			// O10 — one tiny throwaway Generate to force the model resident + GPU layers allocated.
			if (_aiOptions.Value.WarmUpGenerationOnStartup)
			{
				try
				{
					var ai = scope.ServiceProvider.GetRequiredService<IAiGrpcService>();
					await ai.GenerateAsync("ok", maxNewTokens: 1, cancellationToken: timeoutCts.Token);
					_logger.LogInformation("Operator AI warm-up generation issued (model preloaded).");
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					_logger.LogWarning(ex, "Operator AI warm-up generation failed (non-fatal).");
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Shutting down or warm budget elapsed — fine, the first turn just pays the warm.
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Operator AI startup warm threw (non-fatal).");
		}
	}
}
