using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace BeDemo.Api.Utils;

/// <summary>
/// Builds stable partition keys for ASP.NET Core fixed-window rate limiters.
/// </summary>
/// <remarks>
/// <para>
/// Production uses the client IP (or connection id fallback) so each remote host has its own counter.
/// </para>
/// <para>
/// In <c>Testing</c>, each <c>WebApplicationFactory</c> host sets
/// <see cref="TestingScopeConfigurationKey"/> so parallel integration-test
/// hosts do not share permit counters for the same policy name.
/// </para>
/// </remarks>
public static class RateLimitingPartitionKey
{
	/// <summary>Configuration key set once per test host in <c>BeDemo.Api.Tests</c>.</summary>
	public const string TestingScopeConfigurationKey = "Testing:RateLimitScopeId";

	/// <summary>
	/// Resolves the partition key for the current HTTP request.
	/// </summary>
	public static string ForHttpContext(HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
		var remote = httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Connection.Id;
		var identity = string.IsNullOrEmpty(userId) ? $"ip:{remote}" : $"user:{userId}";
		var testScope = httpContext.RequestServices.GetService<IConfiguration>()?[TestingScopeConfigurationKey];
		return string.IsNullOrWhiteSpace(testScope) ? identity : $"{testScope}:{identity}";
	}
}
