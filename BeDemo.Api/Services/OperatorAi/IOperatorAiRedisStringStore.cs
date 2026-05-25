namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Minimal Redis string API used by live stats bundle cache (testable via in-memory fake).</summary>
public interface IOperatorAiRedisStringStore
{
	Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>SET key value NX EX seconds — returns true when lock acquired.</summary>
	Task<bool> SetNotExistsAsync(
		string key,
		string value,
		int expirySeconds,
		CancellationToken cancellationToken = default);

	Task SetWithExpiryMillisecondsAsync(
		string key,
		string value,
		long expiryMilliseconds,
		CancellationToken cancellationToken = default);

	/// <summary>Lua compare-and-delete — returns true when lock was released.</summary>
	Task<bool> CompareAndDeleteAsync(
		string key,
		string token,
		CancellationToken cancellationToken = default);
}
