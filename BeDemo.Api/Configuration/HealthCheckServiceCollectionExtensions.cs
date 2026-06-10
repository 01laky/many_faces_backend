using BeDemo.Api.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the X14 health probes.
/// Registers the database readiness check (tagged <c>ready</c>); the two probe endpoints (<c>/health/live</c>,
/// <c>/health/ready</c>) are mapped in the request pipeline. Behaviour is identical to the inline registration.
/// </summary>
public static class HealthCheckServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesHealthChecks(this IServiceCollection services)
	{
		services.AddHealthChecks()
			.AddCheck<DatabaseReadinessHealthCheck>(
				"database",
				failureStatus: HealthStatus.Unhealthy,
				tags: new[] { "ready" });

		return services;
	}
}
