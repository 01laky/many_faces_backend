using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Performance a load testy
/// </summary>
public class PerformanceEdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public PerformanceEdgeCaseTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Register_ShouldHandle100ConcurrentRegistrations()
	{
		var tasks = Enumerable.Range(0, 100).Select(_ =>
			IntegrationTestRegistration.CompleteRegistrationAsync(
				_client,
				_factory,
				$"test_{Guid.NewGuid()}@test.com",
				"Test1234!@##"));
		var results = await Task.WhenAll(tasks);
		results.Count(r => !string.IsNullOrEmpty(r.AccessToken)).Should().Be(100);
	}

	[Fact]
	public async Task Register_ShouldRespondWithinReasonableTime()
	{
		var stopwatch = Stopwatch.StartNew();
		var body = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1234!@##");
		stopwatch.Stop();

		body.AccessToken.Should().NotBeNullOrEmpty();
		stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
	}

	[Fact]
	public async Task Register_ShouldHandleRapidSequentialRegistrations()
	{
		for (var i = 0; i < 50; i++)
		{
			var body = await IntegrationTestRegistration.CompleteRegistrationAsync(
				_client,
				_factory,
				$"test_{Guid.NewGuid()}@test.com",
				"Test1234!@##");
			body.AccessToken.Should().NotBeNullOrEmpty();
		}
	}

	public void Dispose() => _client.Dispose();
}
