using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

public sealed record OperatorAiSystemSettingsValues(
    bool AiEnabled,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId,
    DateTime? LastEnabledAtUtc,
    string? LastEnableHealthStatus);

/// <summary>L1 + PostgreSQL singleton for the global AI master switch.</summary>
public interface IOperatorAiSystemSettingsProvider
{
    Task<OperatorAiSystemSettingsValues> GetAsync(CancellationToken cancellationToken = default);

    Task<bool> IsAiEnabledAsync(CancellationToken cancellationToken = default);

    Task<OperatorAiSystemSettingsValues> SetAsync(
        OperatorAiSystemSettingsValues values,
        CancellationToken cancellationToken = default);

    OperatorAiSystemSettingsDto ToDto(OperatorAiSystemSettingsValues values);
}
