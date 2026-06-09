using System.Net;
using BeDemo.Api.Data;
using BeDemo.Api.HealthChecks;
using BeDemo.Api.Utils;
using BeDemo.Api.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace BeDemo.Api.Tests.HealthChecks;

/// <summary>
/// X14 health probes. Covers the routing exemption (probes answer without a face prefix), the readiness check's
/// reachable/failed branches, and the two anonymous endpoints through the real pipeline (no token, default-deny
/// fallback must not 401 them).
/// </summary>
[Trait("Category", "BackendInfra")]
public sealed class HealthCheckRoutingTests
{
	[Theory]
	[InlineData("/health")]
	[InlineData("/health/live")]
	[InlineData("/health/ready")]
	public void HealthPaths_AreExemptFromFaceScope(string path)
	{
		Routing.IsExemptFromFaceScope(path).Should().BeTrue();
	}

	[Fact]
	public void HealthPath_IsNotTreatedAsReservedApiPath()
	{
		// /health is neither /api/* nor /hubs/* — RoutingMiddleware must not 400 it as a missing-face-prefix path.
		Routing.IsReservedPathWithoutFacePrefix("/health/live").Should().BeFalse();
	}
}

[Trait("Category", "BackendInfra")]
public sealed class DatabaseReadinessHealthCheckTests
{
	[Fact]
	public async Task ReturnsHealthy_WhenDatabaseReachable()
	{
		using var db = InMemoryDb.Fresh();
		var check = new DatabaseReadinessHealthCheck(db);

		var result = await check.CheckHealthAsync(new HealthCheckContext());

		result.Status.Should().Be(HealthStatus.Healthy);
	}

	[Fact]
	public async Task ReturnsUnhealthy_WhenConnectivityCheckThrows()
	{
		var db = InMemoryDb.Fresh();
		db.Dispose(); // a disposed context makes CanConnectAsync throw → the catch must degrade to Unhealthy, not bubble.
		var check = new DatabaseReadinessHealthCheck(db);

		var result = await check.CheckHealthAsync(new HealthCheckContext());

		result.Status.Should().Be(HealthStatus.Unhealthy);
	}
}

[Trait("Category", "BackendInfra")]
public sealed class HealthCheckEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public HealthCheckEndpointIntegrationTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Liveness_IsAnonymousAndHealthy()
	{
		using var client = _factory.CreateUnscopedClient();

		using var response = await client.GetAsync("/health/live");

		response.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must answer anonymously (no 401 from default-deny)");
		(await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
	}

	[Fact]
	public async Task Readiness_IsAnonymousAndHealthy_WithReachableDatabase()
	{
		using var client = _factory.CreateUnscopedClient();

		using var response = await client.GetAsync("/health/ready");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
	}
}
