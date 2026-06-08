using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// The legacy LLM planner, now demoted to the degraded fallback selector (§6/D12). Given an operator message it
/// asks the local model to pick bundle indices over the in-memory catalog, then supplements with deterministic
/// keyword routes. Used by <see cref="IOperatorAiRetriever"/> when embed/ES is down or the index is not ready
/// (§17.4), and as zero-hit escalation attempt 2 (§6.1).
///
/// <para>Inputs/outputs:</para>
/// message → up-to-MaxSelectedBundleIndices catalog indices (catalog order). Never throws: an empty list means
/// the planner could not pick anything (caller escalates / refuses).
/// </summary>
public interface IOperatorAiPlannerFallbackSelector
{
	Task<IReadOnlyList<int>> SelectBundleIndicesAsync(string userMessage, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class OperatorAiPlannerFallbackSelector : IOperatorAiPlannerFallbackSelector
{
	private readonly IAiGrpcService _ai;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiPlannerFallbackSelector> _logger;

	public OperatorAiPlannerFallbackSelector(
		IAiGrpcService ai,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiPlannerFallbackSelector> logger)
	{
		_ai = ai;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<int>> SelectBundleIndicesAsync(string userMessage, CancellationToken cancellationToken = default)
	{
		var catalog = OperatorAiEntityBundleCatalog.ToPlannerCatalogDto();
		try
		{
			var prompt = OperatorAiLiveStatsPlanner.BuildPrompt(userMessage, catalog);
			var raw = await _ai.GenerateAsync(
				prompt,
				_options.LivePlannerMaxNewTokens,
				responseLocale: "en",
				cancellationToken: cancellationToken);

			var parsed = OperatorAiLiveStatsPlanner.ParseIndices(
				raw,
				OperatorAiEntityBundleCatalog.BundleCount,
				_options.MaxSelectedBundleIndices,
				metricsLike: true);

			// Keyword routes recover planner misses (e.g. "users + chat rooms") deterministically.
			var supplemented = OperatorAiLiveStatsPlanner.SupplementIndicesFromMessage(
				userMessage,
				parsed.Indices,
				OperatorAiEntityBundleCatalog.BundleCount,
				_options.MaxSelectedBundleIndices);

			return supplemented;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Planner fallback selection failed.");
			return Array.Empty<int>();
		}
	}
}
