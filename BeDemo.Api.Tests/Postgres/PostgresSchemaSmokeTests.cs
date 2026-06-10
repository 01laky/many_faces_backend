using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>Smoke test: the model builds a real Postgres schema and is reachable (validates the Testcontainers lane).</summary>
[Trait("Category", "Postgres")]
[Collection("Postgres")]
public sealed class PostgresSchemaSmokeTests
{
	private readonly PostgresFixture _pg;

	public PostgresSchemaSmokeTests(PostgresFixture pg) => _pg = pg;

	[Fact]
	public async Task Model_creates_schema_and_is_reachable()
	{
		await using var ctx = _pg.CreateContext();

		(await ctx.Database.CanConnectAsync()).Should().BeTrue();
		await ctx.Database.EnsureCreatedAsync();

		// A trivial query proves the schema exists and round-trips.
		(await ctx.Faces.CountAsync()).Should().BeGreaterThanOrEqualTo(0);
	}
}
