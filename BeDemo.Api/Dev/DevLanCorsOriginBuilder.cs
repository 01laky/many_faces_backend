using System.Net;

namespace BeDemo.Api.Dev;

/// <summary>
/// Builds additional CORS origins for SPA dev entrypoints on the Mac LAN IPv4 (portal/admin nginx proxies).
/// </summary>
public static class DevLanCorsOriginBuilder
{
	private static readonly (string Scheme, int Port)[] LanDevPorts =
	[
		("http", 9080),
		("http", 8090),
		("http", 8000),
		("https", 9081),
		("https", 8091),
		("https", 8001),
		("https", 8082),
	];

	public static string[] Build(string? lanHost)
	{
		var host = lanHost?.Trim();
		if (string.IsNullOrEmpty(host) || !IPAddress.TryParse(host, out _))
			return [];

		return LanDevPorts
			.Select(p => $"{p.Scheme}://{host}:{p.Port}")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}
}
