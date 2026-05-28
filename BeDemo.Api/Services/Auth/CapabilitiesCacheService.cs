using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Auth;

/// <summary>BE-RP26 — short-TTL cache for <see cref="AccessCapabilitiesService"/>.</summary>
public interface ICapabilitiesCacheService
{
	Task<CapabilitiesResponse> GetCapabilitiesAsync(
		string userId,
		ClaimsPrincipal principal,
		CancellationToken cancellationToken = default);

	void Invalidate(string userId);
}

public sealed class CapabilitiesCacheService : ICapabilitiesCacheService, IAccessCapabilitiesService
{
	private readonly IAccessCapabilitiesService _inner;
	private readonly IMemoryCache _cache;
	private readonly IFaceScopeContext _faceScope;
	private readonly PerformanceOptions _options;

	public CapabilitiesCacheService(
		AccessCapabilitiesService inner,
		IMemoryCache cache,
		IFaceScopeContext faceScope,
		IOptions<PerformanceOptions> options)
	{
		_inner = inner;
		_cache = cache;
		_faceScope = faceScope;
		_options = options.Value;
	}

	private string CacheKey(string userId)
	{
		var faceId = _faceScope.IsAvailable ? _faceScope.FaceId : 0;
		var admin = _faceScope.IsAdminFaceScope;
		return $"cap:{userId}:{faceId}:{admin}";
	}

	public async Task<CapabilitiesResponse> GetCapabilitiesAsync(
		string userId,
		ClaimsPrincipal principal,
		CancellationToken cancellationToken = default)
	{
		var key = CacheKey(userId);
		if (_cache.TryGetValue(key, out CapabilitiesResponse? cached) && cached is not null)
			return cached;

		var dto = await _inner.GetCapabilitiesAsync(userId, principal, cancellationToken).ConfigureAwait(false);
		var ttl = TimeSpan.FromSeconds(Math.Max(5, _options.CapabilitiesCacheSeconds));
		_cache.Set(key, dto, ttl);
		return dto;
	}

	public void Invalidate(string userId)
	{
		if (string.IsNullOrEmpty(userId))
			return;
		// Prefix eviction: IMemoryCache has no prefix remove — invalidate common face scopes lazily via short TTL.
		// Explicit keys cleared when face scope known:
		if (_faceScope.IsAvailable)
		{
			_cache.Remove($"cap:{userId}:{_faceScope.FaceId}:False");
			_cache.Remove($"cap:{userId}:{_faceScope.FaceId}:True");
		}
		_cache.Remove($"cap:{userId}:0:False");
		_cache.Remove($"cap:{userId}:0:True");
	}
}
