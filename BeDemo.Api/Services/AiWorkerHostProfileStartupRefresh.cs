using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Refreshes AI worker host profile on backend startup with bounded retry/backoff.
/// </summary>
public sealed class AiWorkerHostProfileStartupRefresh : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<AiServiceOptions> _options;
    private readonly ILogger<AiWorkerHostProfileStartupRefresh> _logger;

    public AiWorkerHostProfileStartupRefresh(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<AiServiceOptions> options,
        ILogger<AiWorkerHostProfileStartupRefresh> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsEnvironment("Testing") || !_options.Value.HostProfileRefreshOnStartup)
            return;

        using var scope = _scopeFactory.CreateScope();
        var systemSettings = scope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();
        if (!await systemSettings.IsAiEnabledAsync(stoppingToken))
            return;

        var timeoutSeconds = Math.Max(5, _options.Value.HostProfileStartupTimeoutSeconds);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var delay = TimeSpan.FromSeconds(2);

        while (!stoppingToken.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            try
            {
                using var refreshScope = _scopeFactory.CreateScope();
                var svc = refreshScope.ServiceProvider.GetRequiredService<IAiWorkerHostProfileService>();
                await svc.RefreshFromWorkerAsync(stoppingToken);

                var view = await svc.GetOperatorViewAsync(stoppingToken);
                if (view.Reachable)
                {
                    _logger.LogInformation("AI worker host profile startup refresh succeeded");
                    return;
                }

                _logger.LogWarning(
                    "AI worker host profile startup refresh attempt failed: {Error}",
                    view.LastRefreshError ?? "unknown");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI worker host profile startup refresh threw");
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay, stoppingToken);
        }

        _logger.LogWarning(
            "AI worker host profile startup refresh gave up after {Seconds}s",
            timeoutSeconds);
    }
}
