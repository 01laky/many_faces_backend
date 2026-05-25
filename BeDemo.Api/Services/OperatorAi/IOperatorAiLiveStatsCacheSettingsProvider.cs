namespace BeDemo.Api.Services.OperatorAi;

using BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>
/// Server-side global TTL for live stats Redis bundle cache (L1 memory → PostgreSQL → options).
/// </summary>
public interface IOperatorAiLiveStatsCacheSettingsProvider
{
	Task<long> GetTtlMillisecondsAsync(CancellationToken cancellationToken = default);

	Task<long> SetTtlMillisecondsAsync(
		long ttlMilliseconds,
		string? updatedByUserId,
		CancellationToken cancellationToken = default);

	OperatorAiLiveStatsCacheSettingsDto ToDto(long ttlMilliseconds);
}
