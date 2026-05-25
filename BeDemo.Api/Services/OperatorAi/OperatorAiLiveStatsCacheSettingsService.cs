using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// L1 in-process cache for live stats Redis TTL settings only — bundle JSON remains in Redis (L2).
/// Read path: L1 → PostgreSQL singleton → <see cref="OperatorAiOptions.LiveBundleCacheTtlMilliseconds"/>.
/// PUT updates PostgreSQL and refreshes L1 immediately on this process (read-your-writes).
/// </summary>
public sealed class OperatorAiLiveStatsCacheSettingsService : IOperatorAiLiveStatsCacheSettingsProvider
{
	/// <summary>Global cache key — platform-wide TTL, not per-operator.</summary>
	private const string TtlMemoryCacheKey = "OperatorAi:LiveStatsCache:TtlMilliseconds";

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IMemoryCache _memoryCache;
	private readonly OperatorAiOptions _options;

	public OperatorAiLiveStatsCacheSettingsService(
		IServiceScopeFactory scopeFactory,
		IMemoryCache memoryCache,
		IOptions<OperatorAiOptions> options)
	{
		_scopeFactory = scopeFactory;
		_memoryCache = memoryCache;
		_options = options.Value;
	}

	/// <inheritdoc />
	public async Task<long> GetTtlMillisecondsAsync(CancellationToken cancellationToken = default)
	{
		if (_memoryCache.TryGetValue(TtlMemoryCacheKey, out long cached))
			return cached;

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorAiLiveStatsCacheSettings
			.AsNoTracking()
			.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

		var ttlMs = row?.TtlMilliseconds ?? _options.LiveBundleCacheTtlMilliseconds;
		CacheTtl(ttlMs);
		return ttlMs;
	}

	/// <inheritdoc />
	public async Task<long> SetTtlMillisecondsAsync(
		long ttlMilliseconds,
		string? updatedByUserId,
		CancellationToken cancellationToken = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorAiLiveStatsCacheSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
		if (row == null)
		{
			row = new OperatorAiLiveStatsCacheSettings { Id = 1 };
			db.OperatorAiLiveStatsCacheSettings.Add(row);
		}

		row.TtlMilliseconds = ttlMilliseconds;
		row.UpdatedAtUtc = DateTime.UtcNow;
		row.UpdatedByUserId = updatedByUserId;
		await db.SaveChangesAsync(cancellationToken);

		// Immediate read-your-writes for prefetch on this backend instance.
		CacheTtl(ttlMilliseconds);
		return ttlMilliseconds;
	}

	/// <inheritdoc />
	public OperatorAiLiveStatsCacheSettingsDto ToDto(long ttlMilliseconds) => new()
	{
		TtlMilliseconds = ttlMilliseconds,
		DefaultTtlMilliseconds = OperatorAiLiveStatsCacheConstraints.DefaultTtlMilliseconds,
		MinTtlMilliseconds = OperatorAiLiveStatsCacheConstraints.MinTtlMilliseconds,
		MaxTtlMilliseconds = OperatorAiLiveStatsCacheConstraints.MaxTtlMilliseconds,
	};

	private void CacheTtl(long ttlMs)
	{
		var seconds = Math.Max(1, _options.LiveBundleCacheSettingsMemoryCacheSeconds);
		_memoryCache.Set(
			TtlMemoryCacheKey,
			ttlMs,
			new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds),
			});
	}
}
