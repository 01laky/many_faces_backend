using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>
/// Schema/migration drift guard (backend-refactor Phase 4). Applies the full EF migration history to a fresh Postgres
/// database and asserts the resulting schema matches the current model — i.e. no model change is missing a migration.
/// This is the check the InMemory provider cannot give and is the reason a Postgres lane exists.
/// </summary>
[Trait("Category", "Postgres")]
[Collection("Postgres")]
public sealed class SchemaMigrationDriftTests
{
	private readonly PostgresFixture _pg;

	public SchemaMigrationDriftTests(PostgresFixture pg) => _pg = pg;

	[Fact]
	public async Task Migrations_apply_cleanly_and_model_has_no_pending_changes()
	{
		await using var ctx = await _pg.CreateContextInNewDatabaseAsync("drift_" + Guid.NewGuid().ToString("N"));

		// Apply every migration to the empty database — this also proves the migrations run end-to-end on real Postgres.
		await ctx.Database.MigrateAsync();

		ctx.Database.HasPendingModelChanges().Should().BeFalse(
			"the EF model and the committed migrations must agree — a pending change means a migration is missing for a model edit");
	}
}
