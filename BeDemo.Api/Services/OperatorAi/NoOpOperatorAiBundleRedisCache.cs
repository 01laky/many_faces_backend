namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Testing / no-Redis fallback — always loads from DB via the supplied loader delegate.</summary>
public sealed class NoOpOperatorAiBundleRedisCache : IOperatorAiBundleRedisCache
{
	/// <inheritdoc />
	public bool IsRedisBacked => false;

	/// <inheritdoc />
	public void BeginPrefetchRequest() { }

	/// <inheritdoc />
	public async Task<OperatorAiBundleRedisLoadResult> GetOrLoadAsync(
		int index,
		long ttlMs,
		Func<CancellationToken, Task<string>> loadJsonAsync,
		TimeSpan waitTimeout,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var json = await loadJsonAsync(cancellationToken);
			return new OperatorAiBundleRedisLoadResult(true, json, CacheHit: false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return new OperatorAiBundleRedisLoadResult(false, null, CacheHit: false);
		}
	}
}
