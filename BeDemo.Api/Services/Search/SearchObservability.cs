namespace BeDemo.Api.Services.Search;

/// <summary>Structured search metrics logs (§6.5 observability).</summary>
public static class SearchObservability
{
	private static DateTime? _lastReconciliationSuccessUtc;
	private static readonly object ReconciliationLock = new();

	public static DateTime? LastReconciliationSuccessUtc
	{
		get
		{
			lock (ReconciliationLock)
				return _lastReconciliationSuccessUtc;
		}
	}

	public static void RecordReconciliationSuccess(DateTime utcNow)
	{
		lock (ReconciliationLock)
			_lastReconciliationSuccessUtc = utcNow;
	}

	public static void LogAutocompleteRequest(
		ILogger logger,
		string query,
		int offset,
		int pageSize,
		long durationMs,
		int hitCount)
	{
		logger.LogInformation(
			"search.autocomplete duration_ms={DurationMs} page_size={PageSize} offset={Offset} hit_count={HitCount} query_len={QueryLen}",
			durationMs,
			pageSize,
			offset,
			hitCount,
			query.Length);
	}

	public static void LogOutboxPendingCount(ILogger logger, int pendingCount, int warningThreshold)
	{
		logger.LogInformation("search.outbox.pending_count={PendingCount}", pendingCount);
		if (pendingCount > warningThreshold)
		{
			logger.LogWarning(
				"search.outbox.pending_count exceeded threshold: pending={PendingCount} threshold={Threshold}",
				pendingCount,
				warningThreshold);
		}
	}

	public static void LogReconciliationComplete(
		ILogger logger,
		int indexed,
		int deleted,
		int failed,
		long durationMs,
		string correlationId)
	{
		RecordReconciliationSuccess(DateTime.UtcNow);
		logger.LogInformation(
			"search.reconciliation indexed={Indexed} deleted={Deleted} failed={Failed} duration_ms={DurationMs} correlationId={CorrelationId}",
			indexed,
			deleted,
			failed,
			durationMs,
			correlationId);

		if (failed > 0)
		{
			logger.LogWarning(
				"search.reconciliation partial failure indexed={Indexed} deleted={Deleted} failed={Failed} correlationId={CorrelationId}",
				indexed,
				deleted,
				failed,
				correlationId);
		}
	}

	public static void LogReconciliationStaleWarningIfNeeded(ILogger logger, int staleHours = 7)
	{
		var last = LastReconciliationSuccessUtc;
		if (last is null)
			return;

		if (DateTime.UtcNow - last.Value > TimeSpan.FromHours(staleHours))
		{
			logger.LogWarning(
				"search.reconciliation.last_success_utc stale: last_success_utc={LastSuccessUtc}",
				last.Value);
		}
	}
}
