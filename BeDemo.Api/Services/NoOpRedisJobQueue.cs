namespace BeDemo.Api.Services;

/// <summary>Used when Redis is not configured (e.g. tests).</summary>
public sealed class NoOpRedisJobQueue : IRedisJobQueue
{
	public Task EnqueueAsync(string jobType, string payloadJson, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task ScheduleAsync(string jobType, string payloadJson, DateTime runAtUtc, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}
