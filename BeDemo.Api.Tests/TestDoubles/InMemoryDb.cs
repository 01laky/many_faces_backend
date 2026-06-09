using BeDemo.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests.TestDoubles;

/// <summary>
/// Builds throwaway in-memory <see cref="ApplicationDbContext"/> instances sharing a database name, for fast unit
/// tests of EF-touching services. NOTE (backend-refactor §5.2/§10.7): the InMemory provider does NOT enforce unique
/// indexes, FK/cascade, or check constraints — those invariants need the Postgres/Testcontainers lane.
/// </summary>
internal static class InMemoryDb
{
	/// <summary>A fresh context over a unique database (isolated per test).</summary>
	public static ApplicationDbContext Fresh() => Named($"test-{Guid.NewGuid():N}");

	/// <summary>A context over a named database (use the same name to share data across contexts in one test).</summary>
	public static ApplicationDbContext Named(string databaseName) =>
		new(new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(databaseName)
			.Options);
}
