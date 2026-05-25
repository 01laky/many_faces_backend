using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Optional non-blocking warm of all 61 live stats bundle keys after backend start.
/// Mirrors <see cref="AiWorkerHostProfileStartupRefresh"/> — does not block Kestrel/SignalR.
/// </summary>
public sealed class OperatorAiLiveBundleCacheStartupWarm : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IHostEnvironment _environment;
	private readonly IOptions<OperatorAiOptions> _options;
	private readonly ILogger<OperatorAiLiveBundleCacheStartupWarm> _logger;

	public OperatorAiLiveBundleCacheStartupWarm(
		IServiceScopeFactory scopeFactory,
		IHostEnvironment environment,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiLiveBundleCacheStartupWarm> logger)
	{
		_scopeFactory = scopeFactory;
		_environment = environment;
		_options = options;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Skip in tests, when flag is off, or when Redis bundle cache is NoOp.
		if (_environment.IsEnvironment("Testing") || !_options.Value.WarmLiveBundleCacheOnStartup)
			return;

		using var scope = _scopeFactory.CreateScope();
		var systemSettings = scope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();
		if (!await systemSettings.IsAiEnabledAsync(stoppingToken))
			return;

		var delaySeconds = Math.Max(0, _options.Value.WarmLiveBundleCacheStartupDelaySeconds);
		if (delaySeconds > 0)
			await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

		var cache = scope.ServiceProvider.GetRequiredService<IOperatorAiBundleRedisCache>();
		if (!cache.IsRedisBacked)
			return;

		var timeoutSeconds = Math.Max(10, _options.Value.WarmLiveBundleCacheStartupTimeoutSeconds);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

		var started = DateTime.UtcNow;
		try
		{
			var prefetcher = scope.ServiceProvider.GetRequiredService<IOperatorAiLiveStatsPrefetcher>();
			var result = await prefetcher.PrefetchAllAsync(timeoutCts.Token);
			_logger.LogInformation(
				"Live stats bundle cache startup warm completed in {Ms}ms, ready={Ready}, failed={Failed}, cacheHits={CacheHits}, cacheMisses={CacheMisses}",
				(DateTime.UtcNow - started).TotalMilliseconds,
				result.ReadyCount,
				result.FailedCount,
				result.CacheHits,
				result.CacheMisses);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Host shutdown — not a warm failure.
		}
		catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
		{
			// Partial warm is acceptable — remaining indices stay cold until the next live message.
			_logger.LogWarning(
				ex,
				"Live stats bundle cache startup warm timed out after {Seconds}s (partial warm may exist)",
				timeoutSeconds);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Live stats bundle cache startup warm failed");
		}
	}
}
