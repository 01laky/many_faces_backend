using BeDemo.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BeDemo.Api.HealthChecks;

/// <summary>
/// Readiness probe (backend-refactor X14): reports whether the API can actually reach its PostgreSQL database, so an
/// orchestrator only routes traffic to an instance whose backing store is up. Distinct from liveness (process is
/// running) — a live-but-not-ready instance has booted but cannot serve requests yet (e.g. DB still starting).
/// Resolved in a per-check DI scope by the health-check service, so the scoped <see cref="ApplicationDbContext"/> is
/// safe to inject. Failures are swallowed into an Unhealthy result (never throw) and the public endpoint emits only a
/// status word — no connection string or exception detail leaks to an anonymous caller.
/// </summary>
public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
	private readonly ApplicationDbContext _db;

	public DatabaseReadinessHealthCheck(ApplicationDbContext db)
	{
		_db = db;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var canConnect = await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
			return canConnect
				? HealthCheckResult.Healthy("Database reachable")
				: HealthCheckResult.Unhealthy("Database unreachable");
		}
		catch (Exception ex)
		{
			// Keep the exception out of the response body (logged by the health-check infrastructure instead).
			return HealthCheckResult.Unhealthy("Database connectivity check failed", ex);
		}
	}
}
