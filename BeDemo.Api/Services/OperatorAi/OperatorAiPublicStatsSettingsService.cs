using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// L1 in-process cache for operator AI public stats settings (mode + live parallel cap).
/// Read path: L1 → PostgreSQL singleton → <see cref="OperatorAiOptions"/> fallback.
/// </summary>
public sealed class OperatorAiPublicStatsSettingsService : IOperatorAiPublicStatsSettingsProvider
{
	private const string MemoryCacheKey = "OperatorAi:PublicStatsSettings";

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IMemoryCache _memoryCache;
	private readonly OperatorAiOptions _options;

	public OperatorAiPublicStatsSettingsService(
		IServiceScopeFactory scopeFactory,
		IMemoryCache memoryCache,
		IOptions<OperatorAiOptions> options)
	{
		_scopeFactory = scopeFactory;
		_memoryCache = memoryCache;
		_options = options.Value;
	}

	/// <inheritdoc />
	public async Task<OperatorAiPublicStatsSettingsValues> GetAsync(CancellationToken cancellationToken = default)
	{
		if (_memoryCache.TryGetValue(MemoryCacheKey, out OperatorAiPublicStatsSettingsValues? cached) && cached != null)
			return cached;

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorAiPublicStatsSettings
			.AsNoTracking()
			.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

		var values = row == null
			? FallbackValues()
			: new OperatorAiPublicStatsSettingsValues(
				OperatorAiPublicStatsConstraints.NormalizePublicStatsMode(row.PublicStatsMode),
				ClampParallel(row.LiveMaxParallelBundleCalls));

		Cache(values);
		return values;
	}

	/// <inheritdoc />
	public async Task<OperatorAiPublicStatsSettingsValues> SetAsync(
		OperatorAiPublicStatsSettingsValues values,
		string? updatedByUserId,
		CancellationToken cancellationToken = default)
	{
		var normalized = new OperatorAiPublicStatsSettingsValues(
			OperatorAiPublicStatsConstraints.NormalizePublicStatsMode(values.PublicStatsMode),
			ClampParallel(values.LiveMaxParallelBundleCalls));

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorAiPublicStatsSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
		if (row == null)
		{
			row = new OperatorAiPublicStatsSettings { Id = 1 };
			db.OperatorAiPublicStatsSettings.Add(row);
		}

		row.PublicStatsMode = normalized.PublicStatsMode;
		row.LiveMaxParallelBundleCalls = normalized.LiveMaxParallelBundleCalls;
		row.UpdatedAtUtc = DateTime.UtcNow;
		row.UpdatedByUserId = updatedByUserId;
		await db.SaveChangesAsync(cancellationToken);

		Cache(normalized);
		return normalized;
	}

	/// <inheritdoc />
	public OperatorAiPublicStatsSettingsDto ToDto(OperatorAiPublicStatsSettingsValues values) => new()
	{
		PublicStatsMode = values.PublicStatsMode,
		LiveMaxParallelBundleCalls = values.LiveMaxParallelBundleCalls,
		DefaultPublicStatsMode = OperatorAiPublicStatsConstraints.DefaultPublicStatsMode,
		DefaultLiveMaxParallelBundleCalls = OperatorAiPublicStatsConstraints.DefaultLiveMaxParallelBundleCalls,
		MinLiveMaxParallelBundleCalls = OperatorAiPublicStatsConstraints.MinLiveMaxParallelBundleCalls,
		MaxLiveMaxParallelBundleCalls = OperatorAiPublicStatsConstraints.MaxLiveMaxParallelBundleCalls,
	};

	// Public-stats snapshot parallelism is its own admin-configurable knob with its own default constant; it is
	// decoupled from the operator-chat map parallelism (which 7B-perf O13 lowers to 1 for the local serial GPU).
	private OperatorAiPublicStatsSettingsValues FallbackValues() => new(
		OperatorAiPublicStatsConstraints.DefaultPublicStatsMode,
		ClampParallel(OperatorAiPublicStatsConstraints.DefaultLiveMaxParallelBundleCalls));

	private static int ClampParallel(int value) =>
		Math.Min(
			OperatorAiPublicStatsConstraints.MaxLiveMaxParallelBundleCalls,
			Math.Max(OperatorAiPublicStatsConstraints.MinLiveMaxParallelBundleCalls, value));

	private void Cache(OperatorAiPublicStatsSettingsValues values)
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
}
