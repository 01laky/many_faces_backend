using BeDemo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Background purge of <see cref="Models.RegistrationInvite"/> rows so the table does not grow without bound.
/// Runs on a timer from <see cref="RegistrationInviteOptions.CleanupIntervalMinutes"/>.
/// </summary>
public sealed class RegistrationInviteCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RegistrationInviteOptions> _options;
    private readonly ILogger<RegistrationInviteCleanupHostedService> _logger;

    public RegistrationInviteCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RegistrationInviteOptions> options,
        ILogger<RegistrationInviteCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Registration invite cleanup failed");
            }

            var minutes = Math.Clamp(_options.Value.CleanupIntervalMinutes, 5, 24 * 60);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes invites that are expired, or consumed/revoked older than retention. Exposed for unit tests.
    /// </summary>
    internal async Task<int> RunCleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var consumedCutoff = now.AddDays(-Math.Max(1, _options.Value.ConsumedRetentionDays));

        var stale = await context.RegistrationInvites
            .Where(i =>
                i.ExpiresAtUtc < now
                || (i.ConsumedAtUtc != null && i.ConsumedAtUtc < consumedCutoff)
                || (i.RevokedAtUtc != null && i.RevokedAtUtc < consumedCutoff))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count > 0)
        {
            context.RegistrationInvites.RemoveRange(stale);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var deleted = stale.Count;

        if (deleted > 0)
        {
            _logger.LogInformation("Registration invite cleanup removed {Count} row(s)", deleted);
        }

        return deleted;
    }
}
