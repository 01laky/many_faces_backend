using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>BE-RP14 — cache sender display fields for SignalR hubs.</summary>
public interface IHubUserDisplayCache
{
	Task<(string DisplayName, string? Email)?> GetAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class HubUserDisplayCache : IHubUserDisplayCache
{
	private readonly ApplicationDbContext _context;
	private readonly IMemoryCache _cache;
	private readonly PerformanceOptions _options;

	public HubUserDisplayCache(
		ApplicationDbContext context,
		IMemoryCache cache,
		IOptions<PerformanceOptions> options)
	{
		_context = context;
		_cache = cache;
		_options = options.Value;
	}

	public async Task<(string DisplayName, string? Email)?> GetAsync(string userId, CancellationToken cancellationToken = default)
	{
		var key = $"hub-user:{userId}";
		if (_cache.TryGetValue(key, out (string DisplayName, string? Email) cached))
			return cached;

		var row = await _context.Users
			.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => new { u.FirstName, u.LastName, u.Email })
			.FirstOrDefaultAsync(cancellationToken);

		if (row is null)
			return null;

		var display = $"{row.FirstName ?? ""} {row.LastName ?? ""}".Trim();
		var tuple = (display, row.Email);
		_cache.Set(key, tuple, TimeSpan.FromSeconds(Math.Max(5, _options.HubUserDisplayCacheSeconds)));
		return tuple;
	}
}
