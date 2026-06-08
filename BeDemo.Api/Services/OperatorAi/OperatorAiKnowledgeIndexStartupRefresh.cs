using BeDemo.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Hosted service (§7.2 trigger 1). On boot, when the global AI switch is on and search is enabled, runs
/// <see cref="IOperatorAiKnowledgeIndexer.RebuildAsync"/> with <c>force:false</c> so the RAG index is built (or
/// confirmed current via the content-hash marker) before operators query.
///
/// <para>Non-blocking:</para>
/// The work is fired on a background <see cref="Task.Run"/> after a short delay and never blocks Kestrel/request
/// serving. Failures are swallowed (logged) — retrieval degrades to the legacy planner via the readiness gate
/// (§17.4) until the index is ready, so a slow/absent worker at boot never blocks the operator.
///
/// <para>Inputs/outputs:</para>
/// Reads the global AI switch + search availability; calls the indexer; produces log lines only. Idempotent and
/// single-flight-guarded inside the indexer (§17.5), so overlapping with an admin reindex is safe.
/// </summary>
public sealed class OperatorAiKnowledgeIndexStartupRefresh : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ISearchWorkerKnowledgeClient _knowledge;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiKnowledgeIndexStartupRefresh> _logger;

	public OperatorAiKnowledgeIndexStartupRefresh(
		IServiceScopeFactory scopeFactory,
		ISearchWorkerKnowledgeClient knowledge,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiKnowledgeIndexStartupRefresh> logger)
	{
		_scopeFactory = scopeFactory;
		_knowledge = knowledge;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Let the host finish wiring before we reach out to the worker; this must not delay readiness.
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.WarmLiveBundleCacheStartupDelaySeconds)), stoppingToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (!_knowledge.IsAvailable)
		{
			_logger.LogInformation("Operator AI knowledge startup refresh skipped: search worker disabled.");
			return;
		}

		try
		{
			using var scope = _scopeFactory.CreateScope();

			// Only build when the global AI switch is on (RT-13): no AI ⇒ no embeddings ⇒ no index work.
			var settings = scope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();
			if (!await settings.IsAiEnabledAsync(stoppingToken))
			{
				_logger.LogInformation("Operator AI knowledge startup refresh skipped: global AI is disabled.");
				return;
			}

			var indexer = scope.ServiceProvider.GetRequiredService<IOperatorAiKnowledgeIndexer>();
			var result = await indexer.RebuildAsync(force: false, stoppingToken);
			_logger.LogInformation(
				"Operator AI knowledge startup refresh: indexed={Indexed} failed={Failed} skipped={Skipped} error={Error}",
				result.IndexedCount,
				result.FailedCount,
				result.Skipped,
				result.Error ?? "none");
		}
		catch (OperationCanceledException)
		{
			// Host shutting down — nothing to do.
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Operator AI knowledge startup refresh failed (retrieval will use planner fallback until ready).");
		}
	}
}
