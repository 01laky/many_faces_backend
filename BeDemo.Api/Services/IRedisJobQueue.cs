namespace BeDemo.Api.Services;

/// <summary>
/// Redis-backed job queue (BullMQ-style): immediate enqueue + delayed jobs via sorted set.
/// </summary>
public interface IRedisJobQueue
{
	Task EnqueueAsync(string jobType, string payloadJson, CancellationToken cancellationToken = default);

	/// <summary>Schedule a job to become ready at (UTC) <paramref name="runAtUtc"/>.</summary>
	Task ScheduleAsync(string jobType, string payloadJson, DateTime runAtUtc, CancellationToken cancellationToken = default);
}
