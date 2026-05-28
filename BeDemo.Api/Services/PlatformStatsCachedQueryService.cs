using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>BE-RP4 — IMemoryCache decorator for platform stats reads.</summary>
public sealed class PlatformStatsCachedQueryService : IPlatformStatsQueryService
{
	private readonly PlatformStatsQueryService _inner;
	private readonly IMemoryCache _cache;
	private readonly PerformanceOptions _options;

	public PlatformStatsCachedQueryService(
		PlatformStatsQueryService inner,
		IMemoryCache cache,
		IOptions<PerformanceOptions> options)
	{
		_inner = inner;
		_cache = cache;
		_options = options.Value;
	}

	public async Task<AdminDashboardSummaryDto> GetOperatorDashboardSummaryAsync(CancellationToken cancellationToken = default)
	{
		const string key = "stats:dashboard";
		if (_cache.TryGetValue(key, out AdminDashboardSummaryDto? cached) && cached is not null)
			return cached;

		var dto = await _inner.GetOperatorDashboardSummaryAsync(cancellationToken).ConfigureAwait(false);
		_cache.Set(key, dto, TimeSpan.FromSeconds(Math.Max(5, _options.PlatformStatsCacheSeconds)));
		return dto;
	}

	public async Task<PublicStatsSnapshotDto> GetPublicSnapshotAsync(CancellationToken cancellationToken = default)
	{
		const string key = "stats:public";
		if (_cache.TryGetValue(key, out PublicStatsSnapshotDto? cached) && cached is not null)
			return cached;

		var dto = await _inner.GetPublicSnapshotAsync(cancellationToken).ConfigureAwait(false);
		_cache.Set(key, dto, TimeSpan.FromSeconds(Math.Max(5, _options.PublicStatsCacheSeconds)));
		return dto;
	}

	public Task<OperatorAiTimeseriesHintsDto> GetOperatorAiTimeseriesHintsAsync(CancellationToken cancellationToken = default) =>
		_inner.GetOperatorAiTimeseriesHintsAsync(cancellationToken);
}
