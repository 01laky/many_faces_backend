using StackExchange.Redis;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>StackExchange.Redis adapter for <see cref="IOperatorAiRedisStringStore"/>.</summary>
public sealed class StackExchangeOperatorAiRedisStringStore : IOperatorAiRedisStringStore
{
	private static readonly LuaScript UnlockScript = LuaScript.Prepare(
		"if redis.call('get', @key) == @token then return redis.call('del', @key) else return 0 end");

	private readonly IDatabase _database;

	public StackExchangeOperatorAiRedisStringStore(IConnectionMultiplexer multiplexer)
	{
		_database = multiplexer.GetDatabase();
	}

	/// <inheritdoc />
	public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		var value = await _database.StringGetAsync(key).WaitAsync(cancellationToken);
		return value.HasValue ? value.ToString() : null;
	}

	/// <inheritdoc />
	public async Task<bool> SetNotExistsAsync(
		string key,
		string value,
		int expirySeconds,
		CancellationToken cancellationToken = default)
	{
		return await _database.StringSetAsync(
				key,
				value,
				TimeSpan.FromSeconds(expirySeconds),
				When.NotExists)
			.WaitAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task SetWithExpiryMillisecondsAsync(
		string key,
		string value,
		long expiryMilliseconds,
		CancellationToken cancellationToken = default)
	{
		await _database.StringSetAsync(
				key,
				value,
				TimeSpan.FromMilliseconds(expiryMilliseconds))
			.WaitAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> CompareAndDeleteAsync(
		string key,
		string token,
		CancellationToken cancellationToken = default)
	{
		var result = await UnlockScript.EvaluateAsync(
				_database,
				new { key = (RedisKey)key, token = (RedisValue)token })
			.WaitAsync(cancellationToken);
		return (int)result == 1;
	}
}
