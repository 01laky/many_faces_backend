using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Postgres;

/// <summary>
/// Verifies <see cref="RegistrationInviteCleanupHostedService.RunCleanupAsync"/> against a real Postgres database
/// (Phase 3 X11 — <c>ExecuteDeleteAsync</c> requires a relational provider; InMemory does not support it).
/// </summary>
[Trait("Category", "Postgres")]
[Collection("Postgres")]
public sealed class RegistrationInviteCleanupPostgresTests
{
	private readonly PostgresFixture _pg;

	public RegistrationInviteCleanupPostgresTests(PostgresFixture pg) => _pg = pg;

	[Fact]
	public async Task RunCleanupAsync_DeletesExpiredInvites_ViaExecuteDeleteAsync()
	{
		await using var ctx = await _pg.CreateContextInNewDatabaseAsync("invclean_" + Guid.NewGuid().ToString("N")[..8]);
		await ctx.Database.EnsureCreatedAsync();

		// Seed: one expired invite (should be deleted) and one valid invite (should survive).
		ctx.RegistrationInvites.AddRange(
			new RegistrationInvite
			{
				Id = Guid.NewGuid(),
				Email = "expired@test.invalid",
				NormalizedEmail = "EXPIRED@TEST.INVALID",
				LinkHash = "link-expired",
				CodeHash = "code-expired",
				ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
				CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
				Locale = "en",
			},
			new RegistrationInvite
			{
				Id = Guid.NewGuid(),
				Email = "valid@test.invalid",
				NormalizedEmail = "VALID@TEST.INVALID",
				LinkHash = "link-valid",
				CodeHash = "code-valid",
				ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
				CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
				Locale = "en",
			});
		await ctx.SaveChangesAsync();

		// Wire up the service with the same isolated-DB connection string.
		var connStr = ctx.Database.GetConnectionString()!;
		var services = new ServiceCollection();
		services.AddDbContext<ApplicationDbContext>(o =>
			o.UseNpgsql(connStr)
			 .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
		services.Configure<RegistrationInviteOptions>(o => o.ConsumedRetentionDays = 7);
		await using var provider = services.BuildServiceProvider();

		var sut = new RegistrationInviteCleanupHostedService(
			provider.GetRequiredService<IServiceScopeFactory>(),
			provider.GetRequiredService<IOptions<RegistrationInviteOptions>>(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<RegistrationInviteCleanupHostedService>.Instance);

		var deleted = await sut.RunCleanupAsync(CancellationToken.None);

		deleted.Should().Be(1, "only the expired invite must be removed");
		(await ctx.RegistrationInvites.CountAsync()).Should().Be(1, "the valid invite survives");
		(await ctx.RegistrationInvites.SingleAsync()).Email.Should().Be("valid@test.invalid");
	}
}
