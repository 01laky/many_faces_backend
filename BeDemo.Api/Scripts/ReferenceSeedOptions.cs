using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BeDemo.Api.Scripts;

/// <summary>
/// When <c>Seed:AssumeExternalSqlReferenceApplied</c> is true (e.g. after <c>many_faces_database/scripts/seed-after-migrate.sh</c>),
/// reference rows (roles, page types, faces, OAuth client, …) are expected from SQL; the API skips duplicate C# inserts.
/// In-memory <c>Testing</c> always seeds reference data via EF so unit/integration tests never require <c>psql</c>.
/// </summary>
public static class ReferenceSeedOptions
{
	public const string AssumeExternalSqlReferenceAppliedKey = "Seed:AssumeExternalSqlReferenceApplied";

	public static bool AssumeExternalSqlReferenceApplied(IConfiguration configuration) =>
		configuration.GetValue(AssumeExternalSqlReferenceAppliedKey, false);

	/// <summary>Returns true when the API should run <see cref="DatabaseSeeder"/> reference inserts (roles, types, faces, OAuth client).</summary>
	public static bool ShouldSeedReferenceDataViaApi(IHostEnvironment environment, IConfiguration configuration)
	{
		if (environment.IsEnvironment("Testing"))
			return true;
		return !AssumeExternalSqlReferenceApplied(configuration);
	}
}
