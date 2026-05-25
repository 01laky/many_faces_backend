using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

public sealed record OperatorAiPublicStatsSettingsValues(string PublicStatsMode, int LiveMaxParallelBundleCalls);

/// <summary>Platform-wide operator AI public stats mode and live parallel cap (PostgreSQL singleton + L1).</summary>
public interface IOperatorAiPublicStatsSettingsProvider
{
	Task<OperatorAiPublicStatsSettingsValues> GetAsync(CancellationToken cancellationToken = default);

	Task<OperatorAiPublicStatsSettingsValues> SetAsync(
		OperatorAiPublicStatsSettingsValues values,
		string? updatedByUserId,
		CancellationToken cancellationToken = default);

	OperatorAiPublicStatsSettingsDto ToDto(OperatorAiPublicStatsSettingsValues values);
}
