using Microsoft.Extensions.Caching.Memory;

namespace BeDemo.Api.Services;

/// <summary>
/// Per-user sliding window for <see cref="Hubs.ChatHub.SendToAi"/> (ACL A20) — limits gRPC cost / abuse without affecting other hub methods.
/// </summary>
public interface IChatHubAiRateLimiter
{
	/// <summary>Returns false when the user exceeded the configured budget for the current window.</summary>
	bool TryAllow(string? userId);
}

/// <inheritdoc />
public sealed class ChatHubAiRateLimiter : IChatHubAiRateLimiter
{
	private readonly IMemoryCache _cache;
	private readonly IConfiguration _configuration;

	public ChatHubAiRateLimiter(IMemoryCache cache, IConfiguration configuration)
	{
		_cache = cache;
		_configuration = configuration;
	}

	/// <inheritdoc />
	public bool TryAllow(string? userId)
	{
		if (string.IsNullOrEmpty(userId))
			return false;

		var maxPerWindow = _configuration.GetValue("ChatHub:SendAiMaxPerWindow", 30);
		var windowSeconds = _configuration.GetValue("ChatHub:SendAiWindowSeconds", 60);
		if (maxPerWindow <= 0)
			return true;

		var key = $"chatai:rl:{userId}";
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
