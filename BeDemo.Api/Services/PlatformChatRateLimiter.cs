using Microsoft.Extensions.Caching.Memory;

namespace BeDemo.Api.Services;

/// <summary>Per super-admin sliding window for <see cref="Hubs.MessengerHub.SendPlatformDirectMessage"/>.</summary>
public interface IPlatformChatRateLimiter
{
    bool TryAllow(string? userId);
}

/// <inheritdoc />
public sealed class PlatformChatRateLimiter : IPlatformChatRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public PlatformChatRateLimiter(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public bool TryAllow(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        // Configurable sliding window per super-admin (in-memory; dev/single-node friendly).
        var maxPerWindow = _configuration.GetValue("MessengerHub:PlatformChatMaxPerWindow", 30);
        var windowSeconds = _configuration.GetValue("MessengerHub:PlatformChatWindowSeconds", 60);
        if (maxPerWindow <= 0)
            return true;

        var key = $"platformchat:rl:{userId}";
        var now = DateTime.UtcNow;
        if (!_cache.TryGetValue(key, out RateWindow? state) || state == null || now >= state.WindowEndUtc)
        {
            state = new RateWindow { Count = 1, WindowEndUtc = now.AddSeconds(windowSeconds) };
            _cache.Set(key, state, new MemoryCacheEntryOptions { AbsoluteExpiration = state.WindowEndUtc });
            return true;
        }

        if (state.Count >= maxPerWindow)
            return false;

        state.Count++;
        _cache.Set(key, state, new MemoryCacheEntryOptions { AbsoluteExpiration = state.WindowEndUtc });
        return true;
    }

    private sealed class RateWindow
    {
        public int Count { get; set; }
        public DateTime WindowEndUtc { get; set; }
    }
}
