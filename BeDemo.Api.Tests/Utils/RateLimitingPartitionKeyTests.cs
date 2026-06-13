using System.Net;
using System.Security.Claims;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the rate-limiter partition key (previously untested): authenticated requests
/// partition by user id, anonymous by IP, and the per-test-host scope id prefixes the key when present.
/// </summary>
public sealed class RateLimitingPartitionKeyTests
{
	private static DefaultHttpContext Context(string? userId, string? ip, string? testScope)
	{
		var ctx = new DefaultHttpContext();
		if (userId is not null)
		{
			ctx.User = new ClaimsPrincipal(
				new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"));
		}

		if (ip is not null)
		{
			ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
		}

		// The framework always populates RequestServices in production; DefaultHttpContext leaves it null,
		// so provide a (possibly empty) provider — an empty one returns a null IConfiguration (no scope).
		var services = new ServiceCollection();
		if (testScope is not null)
		{
			var config = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					[RateLimitingPartitionKey.TestingScopeConfigurationKey] = testScope,
				})
				.Build();
			services.AddSingleton<IConfiguration>(config);
		}

		ctx.RequestServices = services.BuildServiceProvider();
		return ctx;
	}

	[Fact]
	public void Anonymous_request_partitions_by_ip()
	{
		RateLimitingPartitionKey.ForHttpContext(Context(null, "203.0.113.7", null))
			.Should().Be("ip:203.0.113.7");
	}

	[Fact]
	public void Authenticated_request_partitions_by_user_id()
	{
		RateLimitingPartitionKey.ForHttpContext(Context("user-42", "203.0.113.7", null))
			.Should().Be("user:user-42");
	}

	[Fact]
	public void Test_scope_id_prefixes_the_partition_key()
	{
		RateLimitingPartitionKey.ForHttpContext(Context("user-42", null, "hostA"))
			.Should().Be("hostA:user:user-42");
	}

	[Fact]
	public void Null_http_context_throws()
	{
		var act = () => RateLimitingPartitionKey.ForHttpContext(null!);
		act.Should().Throw<ArgumentNullException>();
	}
}
