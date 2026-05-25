using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services.OperatorAi;

namespace BeDemo.Api.Tests.Testing;

/// <summary>Configurable in-memory stub for unit tests that construct services directly.</summary>
public sealed class StubOperatorAiSystemSettingsProvider : IOperatorAiSystemSettingsProvider
{
	private OperatorAiSystemSettingsValues _values;

	public StubOperatorAiSystemSettingsProvider(bool aiEnabled = true)
	{
		_values = new OperatorAiSystemSettingsValues(
			aiEnabled,
			DateTime.UtcNow,
			null,
			aiEnabled ? DateTime.UtcNow : null,
			aiEnabled ? "ok" : null);
	}

	public void SetEnabled(bool enabled)
	{
		_values = _values with
		{
			AiEnabled = enabled,
			UpdatedAtUtc = DateTime.UtcNow,
			LastEnabledAtUtc = enabled ? DateTime.UtcNow : _values.LastEnabledAtUtc,
			LastEnableHealthStatus = enabled ? "ok" : _values.LastEnableHealthStatus,
		};
	}

	public Task<OperatorAiSystemSettingsValues> GetAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(_values);

	public Task<bool> IsAiEnabledAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(_values.AiEnabled);

	public Task<OperatorAiSystemSettingsValues> SetAsync(
		OperatorAiSystemSettingsValues values,
		CancellationToken cancellationToken = default)
	{
		_values = values;
		return Task.FromResult(_values);
	}

	public OperatorAiSystemSettingsDto ToDto(OperatorAiSystemSettingsValues values) => new()
	{
		AiEnabled = values.AiEnabled,
		UpdatedAtUtc = values.UpdatedAtUtc,
		UpdatedByUserId = values.UpdatedByUserId,
		LastEnabledAtUtc = values.LastEnabledAtUtc,
		LastEnableHealthStatus = values.LastEnableHealthStatus,
	};
}
