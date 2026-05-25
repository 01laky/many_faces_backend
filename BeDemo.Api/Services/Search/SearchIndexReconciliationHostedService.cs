using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>
/// Startup-delayed and periodic full index reconciliation (§6.2).
/// Single-flight: overlapping ticks are skipped while a run is in progress.
/// </summary>
public sealed class SearchIndexReconciliationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchIndexReconciliationHostedService> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public SearchIndexReconciliationHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<SearchOptions> options,
        ILogger<SearchIndexReconciliationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (_environment.IsEnvironment("Testing") || !opts.IsEnabled || !opts.ReconciliationEnabled)
            return;

        var startupDelay = Math.Max(0, opts.ReconciliationStartupDelaySeconds);
        if (startupDelay > 0)
            await Task.Delay(TimeSpan.FromSeconds(startupDelay), stoppingToken);

        var intervalHours = Math.Max(1, opts.ReconciliationIntervalHours);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        do
        {
            await TryRunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task TryRunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!await _runLock.WaitAsync(0, stoppingToken))
            {
                _logger.LogInformation("Search reconciliation tick skipped — previous run still in progress");
                return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var timeoutMinutes = Math.Max(1, _options.Value.ReconciliationRunTimeoutMinutes);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SearchIndexReconciliationRunner>();
            await runner.RunAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown — swallow cancel.
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Search reconciliation run timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search reconciliation run failed");
        }
        finally
        {
            _runLock.Release();
        }
    }
}
