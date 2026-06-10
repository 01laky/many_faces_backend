using BeDemo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>
/// Testcontainers-backed PostgreSQL fixture (backend-refactor Phase 4 — the Postgres test lane). Spins up a real
/// <c>postgres:16-alpine</c> (matching production) once per collection so tests that need true relational-database
/// semantics — unique indexes, FK enforcement, migration application — can run against Postgres instead of the
/// InMemory provider, which silently ignores constraints. Requires Docker; tests are tagged <c>Category=Postgres</c>
/// so a CI lane can opt in/out.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
		.Build();

	/// <summary>The container connection string (valid after <see cref="InitializeAsync"/>).</summary>
	public string ConnectionString { get; private set; } = string.Empty;

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		ConnectionString = _container.GetConnectionString();
	}

	public async Task DisposeAsync() => await _container.DisposeAsync().AsTask();

	/// <summary>
	/// A fresh <see cref="ApplicationDbContext"/> over the container, configured like production (plain Npgsql, no
	/// interceptors — they are irrelevant to schema/constraint tests). Pending-model-changes warnings are ignored
	/// here exactly as in <c>Program.cs</c> so <c>EnsureCreated</c>/<c>Migrate</c> never throw on that warning.
	/// </summary>
	public ApplicationDbContext CreateContext() => CreateContext(ConnectionString);

	/// <summary>
	/// Creates a brand-new, empty database on the container and returns a context over it. Use this when a test needs
	/// isolation from others sharing the collection (e.g. one EnsureCreated/Migrate per test) so seeded data and
	/// schema do not collide.
	/// </summary>
	public async Task<ApplicationDbContext> CreateContextInNewDatabaseAsync(string databaseName)
	{
		await using (var admin = new Npgsql.NpgsqlConnection(ConnectionString))
		{
			await admin.OpenAsync();
			await using var cmd = admin.CreateCommand();
			cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
			await cmd.ExecuteNonQueryAsync();
		}

		var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName }.ConnectionString;
		return CreateContext(connectionString);
	}

	private static ApplicationDbContext CreateContext(string connectionString)
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseNpgsql(connectionString)
			.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
			.Options;
		return new ApplicationDbContext(options);
	}
}

/// <summary>xUnit collection so the single container is shared across all Postgres-lane test classes.</summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
