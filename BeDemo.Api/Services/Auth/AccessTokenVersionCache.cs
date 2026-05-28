using BeDemo.Api.Configuration;
using BeDemo.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Auth;

public sealed class AccessTokenVersionCache : IAccessTokenVersionCache
{
	private readonly IMemoryCache _cache;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly PerformanceOptions _options;

	public AccessTokenVersionCache(
		IMemoryCache cache,
		UserManager<ApplicationUser> userManager,
		IOptions<PerformanceOptions> options)
	{
		_cache = cache;
		_userManager = userManager;
		_options = options.Value;
	}

	private static string Key(string userId) => AccessTokenVersionCacheKeys.ForUser(userId);

	public async Task<int?> GetVersionAsync(string userId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(userId))
			return null;

		if (_cache.TryGetValue(Key(userId), out int cached))
			return cached;

		var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
		if (user is null)
			return null;

		var ttl = TimeSpan.FromSeconds(Math.Max(5, _options.AccessTokenVersionCacheSeconds));
		_cache.Set(Key(userId), user.AccessTokenVersion, ttl);
		return user.AccessTokenVersion;
	}

	public void Invalidate(string userId)
	{
		if (!string.IsNullOrEmpty(userId))
			_cache.Remove(Key(userId));
	}
}
