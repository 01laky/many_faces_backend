using Microsoft.Extensions.Hosting;

namespace BeDemo.Api.Services;

/// <summary>
/// Background worker that periodically runs <see cref="IContentRetentionCleanupService"/>.
/// Disabled by default; enable with configuration keys under <c>Retention:</c> (see appsettings).
/// </summary>
public sealed class ContentRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContentRetentionHostedService> _logger;

    public ContentRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ContentRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When disabled, the hosted service exits immediately so no timer threads are held in dev/test.
        if (!_configuration.GetValue("Retention:Enabled", false))
            return;

        var intervalHours = Math.Max(1, _configuration.GetValue("Retention:IntervalHours", 24));
        // Execute=false => dry-run only (counts redactions but does not mutate the database).
        var execute = _configuration.GetValue("Retention:Execute", false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IContentRetentionCleanupService>();
                var result = await svc.RunAsync(dryRun: !execute, DateTime.UtcNow, stoppingToken);
                _logger.LogInformation(
                    "ModerationRetentionRun dryRun={DryRun} blogs={Blogs} albums={Albums} reels={Reels}",
                    !execute,
                    result.BlogsRedacted,
                    result.AlbumsRedacted,
                    result.ReelsRedacted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Moderation retention run failed");
            }

            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }
}
