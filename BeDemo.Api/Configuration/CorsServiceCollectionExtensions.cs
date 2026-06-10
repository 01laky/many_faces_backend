using BeDemo.Api.Dev;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for CORS. Under transport
/// hardening only the configured <c>Cors:Origins</c> are allowed; otherwise the dev defaults + LAN origins
/// (<see cref="DevLanCorsOriginBuilder"/>) + configured origins are combined. Moved verbatim, so behaviour is
/// unchanged.
/// </summary>
public static class CorsServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesCors(this IServiceCollection services, IConfiguration configuration, bool useTransportHardening)
	{
		// CORS: default dev origins + optional Cors:Origins[] from configuration (production).
		var defaultCorsOrigins = new[]
		{
	"http://localhost:8081", "http://localhost:8082", "http://localhost:8080", "http://localhost:9080",
	"http://localhost:9081", "https://localhost:8081", "https://localhost:8082", "https://localhost:8080",
	"https://localhost:9080", "https://localhost:9081",
};
		var extraOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
		var devLanHost = configuration["DEV_LAN_HOST"]?.Trim();
		if (string.IsNullOrEmpty(devLanHost))
			devLanHost = Environment.GetEnvironmentVariable("DEV_LAN_HOST")?.Trim();
		var lanOrigins = BeDemo.Api.Dev.DevLanCorsOriginBuilder.Build(devLanHost);
		var corsOriginSource = useTransportHardening
			? extraOrigins
			: defaultCorsOrigins.Concat(lanOrigins).Concat(extraOrigins);
		var corsOrigins = corsOriginSource
			.Where(o => !string.IsNullOrWhiteSpace(o))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		services.AddCors(options =>
		{
			options.AddDefaultPolicy(policy =>
			{
				policy
					.WithOrigins(corsOrigins)
					.AllowAnyMethod()
					.AllowAnyHeader()
					.AllowCredentials()
					.SetPreflightMaxAge(TimeSpan.FromHours(1));
			});
		});

		return services;
	}
}
