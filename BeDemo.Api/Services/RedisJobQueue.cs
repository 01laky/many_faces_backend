using System.Text.Json;
using StackExchange.Redis;

namespace BeDemo.Api.Services;

/// <summary>
/// Minimal queue: LIST for ready jobs, ZSET for delayed jobs (score = due unix ms).
/// </summary>
public sealed record RedisJobEnvelope(string Id, string Type, string Payload);

public sealed class RedisJobQueue : IRedisJobQueue
{
	public const string ReadyListKey = "bedemo:jobs:ready";
	public const string DelayedZsetKey = "bedemo:jobs:delayed";

	private readonly IConnectionMultiplexer _redis;
	private readonly ILogger<RedisJobQueue> _logger;

	public RedisJobQueue(IConnectionMultiplexer redis, ILogger<RedisJobQueue> logger)
	{
		_redis = redis;
		_logger = logger;
	}

	public async Task EnqueueAsync(string jobType, string payloadJson, CancellationToken cancellationToken = default)
	{
		var envelope = JsonSerializer.Serialize(new RedisJobEnvelope(Guid.NewGuid().ToString("N"), jobType, payloadJson));
		var db = _redis.GetDatabase();
		await db.ListLeftPushAsync(ReadyListKey, envelope);
		_logger.LogDebug("Enqueued job {JobType}", jobType);
	}

	public async Task ScheduleAsync(string jobType, string payloadJson, DateTime runAtUtc, CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid().ToString("N");
		var envelope = JsonSerializer.Serialize(new RedisJobEnvelope(id, jobType, payloadJson));
		var score = new DateTimeOffset(DateTime.SpecifyKind(runAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
		var db = _redis.GetDatabase();
		await db.SortedSetAddAsync(DelayedZsetKey, envelope, score);
		_logger.LogDebug("Scheduled job {JobType} {JobId} at {RunAtUtc}", jobType, id, runAtUtc);
	}

}
