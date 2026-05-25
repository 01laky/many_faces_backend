using System.Collections.Concurrent;
using BeDemo.Api.Services.OperatorAi;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>In-memory Redis string store for unit tests (single-flight, TTL not simulated).</summary>
internal sealed class InMemoryOperatorAiRedisStringStore : IOperatorAiRedisStringStore
{
	private readonly ConcurrentDictionary<string, string> _values = new();
	private readonly ConcurrentDictionary<string, string> _locks = new();

	public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		if (_values.TryGetValue(key, out var value))
			return Task.FromResult<string?>(value);
		return Task.FromResult<string?>(null);
	}

	public Task<bool> SetNotExistsAsync(
		string key,
		string value,
		int expirySeconds,
		CancellationToken cancellationToken = default)
	{
		var acquired = _locks.TryAdd(key, value);
		return Task.FromResult(acquired);
	}

	public Task SetWithExpiryMillisecondsAsync(
		string key,
		string value,
		long expiryMilliseconds,
		CancellationToken cancellationToken = default)
	{
		_values[key] = value;
		return Task.CompletedTask;
	}

	public Task<bool> CompareAndDeleteAsync(
		string key,
		string token,
		CancellationToken cancellationToken = default)
	{
		if (_locks.TryGetValue(key, out var current) && current == token)
			return Task.FromResult(_locks.TryRemove(key, out _));
		return Task.FromResult(false);
	}
}
