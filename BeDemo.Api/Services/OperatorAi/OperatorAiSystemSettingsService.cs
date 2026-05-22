using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// L1 in-process cache for the global AI enable flag.
/// Read path: L1 → PostgreSQL singleton (insert-on-first-read with bootstrap default from options/env).
/// </summary>
public sealed class OperatorAiSystemSettingsService : IOperatorAiSystemSettingsProvider
{
    private const string MemoryCacheKey = "OperatorAi:SystemSettings";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IHostEnvironment _environment;
    private readonly OperatorAiOptions _options;

    public OperatorAiSystemSettingsService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IHostEnvironment environment,
        IOptions<OperatorAiOptions> options)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _environment = environment;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<OperatorAiSystemSettingsValues> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(MemoryCacheKey, out OperatorAiSystemSettingsValues? cached) && cached != null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.OperatorAiSystemSettings
            .SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

        if (row == null)
        {
            row = new OperatorAiSystemSettings
            {
                Id = 1,
                AiEnabled = ResolveBootstrapDefaultEnabled(),
                UpdatedAtUtc = DateTime.UtcNow,
            };
            db.OperatorAiSystemSettings.Add(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        var values = ToValues(row);
        Cache(values);
        return values;
    }

    /// <inheritdoc />
    public async Task<bool> IsAiEnabledAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken).ConfigureAwait(false)).AiEnabled;

    /// <inheritdoc />
    public async Task<OperatorAiSystemSettingsValues> SetAsync(
        OperatorAiSystemSettingsValues values,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.OperatorAiSystemSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
        if (row == null)
        {
            row = new OperatorAiSystemSettings { Id = 1 };
            db.OperatorAiSystemSettings.Add(row);
        }

        row.AiEnabled = values.AiEnabled;
        row.UpdatedAtUtc = values.UpdatedAtUtc;
        row.UpdatedByUserId = values.UpdatedByUserId;
        row.LastEnabledAtUtc = values.LastEnabledAtUtc;
        row.LastEnableHealthStatus = values.LastEnableHealthStatus;
        await db.SaveChangesAsync(cancellationToken);

        Cache(values);
        return values;
    }

    /// <inheritdoc />
    public OperatorAiSystemSettingsDto ToDto(OperatorAiSystemSettingsValues values) => new()
    {
        AiEnabled = values.AiEnabled,
        UpdatedAtUtc = values.UpdatedAtUtc,
        UpdatedByUserId = values.UpdatedByUserId,
        LastEnabledAtUtc = values.LastEnabledAtUtc,
        LastEnableHealthStatus = values.LastEnableHealthStatus,
    };

    private static OperatorAiSystemSettingsValues ToValues(OperatorAiSystemSettings row) => new(
        row.AiEnabled,
        row.UpdatedAtUtc,
        row.UpdatedByUserId,
        row.LastEnabledAtUtc,
        row.LastEnableHealthStatus);

    /// <summary>
    /// First-row insert only: Testing always false; env OPERATOR_AI_ENABLED_ON_FIRST_BOOT overrides options.
    /// </summary>
    private bool ResolveBootstrapDefaultEnabled()
    {
        if (_environment.IsEnvironment("Testing"))
            return false;

        var env = Environment.GetEnvironmentVariable("OPERATOR_AI_ENABLED_ON_FIRST_BOOT");
        if (!string.IsNullOrWhiteSpace(env) && bool.TryParse(env.Trim(), out var fromEnv))
            return fromEnv;

        return _options.DefaultAiEnabled;
    }

    private void Cache(OperatorAiSystemSettingsValues values)
    {
        var seconds = Math.Max(1, _options.LiveBundleCacheSettingsMemoryCacheSeconds);
        _memoryCache.Set(
            MemoryCacheKey,
            values,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds),
            });
    }

    /// <summary>Invalidates L1 after enable/disable writes.</summary>
    public void InvalidateCache() => _memoryCache.Remove(MemoryCacheKey);
}
