using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Outcome of PUT enable — includes stable error codes for admin UI mapping.</summary>
public sealed record OperatorAiEnableOutcome(
	bool Success,
	string? ErrorCode,
	OperatorAiSystemSettingsDto? Settings);

/// <summary>
/// Orchestrates Activate AI: health poll (Ready/Loading), persist enabled, warm Redis bundle cache.
/// Uses the unguarded <see cref="AiGrpcService"/> so health is not blocked by the availability decorator.
/// </summary>
public interface IOperatorAiEnableService
{
	Task<OperatorAiEnableOutcome> EnableAsync(string? updatedByUserId, CancellationToken cancellationToken = default);

	Task<OperatorAiSystemSettingsDto> DisableAsync(string? updatedByUserId, CancellationToken cancellationToken = default);
}

public sealed class OperatorAiEnableService : IOperatorAiEnableService
{
	public const string ErrorWorkerUnreachable = "worker_unreachable";
	public const string ErrorModelLoadingTimeout = "model_loading_timeout";

	private readonly IOperatorAiSystemSettingsProvider _settings;
	private readonly IAiModelStatusClient _modelStatus;
	private readonly IOperatorAiLiveStatsPrefetcher _prefetcher;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiEnableService> _logger;

	public OperatorAiEnableService(
		IOperatorAiSystemSettingsProvider settings,
		IAiModelStatusClient modelStatus,
		IOperatorAiLiveStatsPrefetcher prefetcher,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiEnableService> logger)
	{
		_settings = settings;
		_modelStatus = modelStatus;
		_prefetcher = prefetcher;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<OperatorAiEnableOutcome> EnableAsync(
		string? updatedByUserId,
		CancellationToken cancellationToken = default)
	{
		var current = await _settings.GetAsync(cancellationToken);
		if (current.AiEnabled)
			return new OperatorAiEnableOutcome(true, null, _settings.ToDto(current));

		var health = await WaitForModelReadyAsync(cancellationToken);
		if (!health.Success)
		{
			return new OperatorAiEnableOutcome(
				false,
				health.ErrorCode,
				_settings.ToDto(current));
		}

		var now = DateTime.UtcNow;
		var saved = await _settings.SetAsync(
			new OperatorAiSystemSettingsValues(
				AiEnabled: true,
				UpdatedAtUtc: now,
				UpdatedByUserId: updatedByUserId,
				LastEnabledAtUtc: now,
				LastEnableHealthStatus: "ok"),
			cancellationToken);
		await WarmLiveBundleCacheAsync(cancellationToken);

		return new OperatorAiEnableOutcome(true, null, _settings.ToDto(saved));
	}

	/// <inheritdoc />
	public async Task<OperatorAiSystemSettingsDto> DisableAsync(
		string? updatedByUserId,
		CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var current = await _settings.GetAsync(cancellationToken);
		var saved = await _settings.SetAsync(
			new OperatorAiSystemSettingsValues(
				AiEnabled: false,
				UpdatedAtUtc: now,
				UpdatedByUserId: updatedByUserId,
				LastEnabledAtUtc: current.LastEnabledAtUtc,
				LastEnableHealthStatus: current.LastEnableHealthStatus),
			cancellationToken);
		return _settings.ToDto(saved);
	}

	/// <summary>Polls GetModelStatus until Ready or deadline when Loading.</summary>
	private async Task<(bool Success, string? ErrorCode)> WaitForModelReadyAsync(CancellationToken cancellationToken)
	{
		var waitSeconds = Math.Max(5, _options.EnableHealthLoadingWaitSeconds);
		var pollSeconds = Math.Max(1, _options.EnableHealthPollIntervalSeconds);
		var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			AiModelStatus status;
			try
			{
				status = await _modelStatus.GetModelStatusAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Activate AI health probe failed with exception");
				return (false, ErrorWorkerUnreachable);
			}

			if (status.Ready)
				return (true, null);

			if (status.Unavailable && !status.Loading)
				return (false, ErrorWorkerUnreachable);

			if (DateTime.UtcNow >= deadline)
			{
				if (status.Loading)
					return (false, ErrorModelLoadingTimeout);
				return (false, ErrorWorkerUnreachable);
			}

			await Task.Delay(TimeSpan.FromSeconds(pollSeconds), cancellationToken);
		}
	}

	/// <summary>Prefetch all live-stats bundles into Redis using configured TTL (best-effort).</summary>
	private async Task WarmLiveBundleCacheAsync(CancellationToken cancellationToken)
	{
		var timeoutSeconds = Math.Max(10, _options.WarmLiveBundleCacheStartupTimeoutSeconds);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

		try
		{
			var result = await _prefetcher.PrefetchAllAsync(timeoutCts.Token);
			_logger.LogInformation(
				"Activate AI Redis warm completed: ready={Ready}, failed={Failed}, hits={Hits}, misses={Misses}",
				result.ReadyCount,
				result.FailedCount,
				result.CacheHits,
				result.CacheMisses);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning(
				"Activate AI Redis warm timed out after {Seconds}s (partial warm may exist)",
				timeoutSeconds);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Activate AI Redis warm failed (AI remains enabled)");
		}
	}
}
