using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for <see cref="OAuthClientValidator"/> (O1: DB-backed client credentials).
/// </summary>
public sealed class OAuthClientValidatorTests
{
	private static ApplicationDbContext CreateDb()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"oauth_client_val_{Guid.NewGuid():N}")
			.Options;
		return new ApplicationDbContext(options);
	}

	private static void SeedActiveClient(ApplicationDbContext db, string clientId, string plainSecret, IPasswordHasher<OAuthClient> hasher)
	{
		var entity = new OAuthClient
		{
			ClientId = clientId,
			IsActive = true,
			CreatedAtUtc = DateTime.UtcNow,
		};
		entity.SecretHash = hasher.HashPassword(entity, plainSecret);
		db.OAuthClients.Add(entity);
		db.SaveChanges();
	}

	[Fact]
	public async Task ValidateAsync_ReturnsTrue_WhenClientActiveAndSecretMatches()
	{
		await using var db = CreateDb();
		var hasher = new PasswordHasher<OAuthClient>();
		SeedActiveClient(db, "c1", "secret-1", hasher);
		var sut = new OAuthClientValidator(db, hasher, NullLogger<OAuthClientValidator>.Instance);

		(await sut.ValidateAsync("c1", "secret-1")).Should().BeTrue();
	}

	[Fact]
	public async Task ValidateAsync_ReturnsFalse_WhenClientInactive()
	{
		await using var db = CreateDb();
		var hasher = new PasswordHasher<OAuthClient>();
		var entity = new OAuthClient
		{
			ClientId = "inactive",
			IsActive = false,
			CreatedAtUtc = DateTime.UtcNow,
		};
		entity.SecretHash = hasher.HashPassword(entity, "good-secret");
		db.OAuthClients.Add(entity);
		db.SaveChanges();

		var sut = new OAuthClientValidator(db, hasher, NullLogger<OAuthClientValidator>.Instance);

		(await sut.ValidateAsync("inactive", "good-secret")).Should().BeFalse();
	}

	[Fact]
	public async Task ValidateAsync_ReturnsFalse_WhenUnknownClientId()
	{
		await using var db = CreateDb();
		var hasher = new PasswordHasher<OAuthClient>();
		var sut = new OAuthClientValidator(db, hasher, NullLogger<OAuthClientValidator>.Instance);

		(await sut.ValidateAsync("missing", "x")).Should().BeFalse();
	}

	[Fact]
	public async Task ValidateAsync_ReturnsFalse_WhenSecretWrong()
	{
		await using var db = CreateDb();
		var hasher = new PasswordHasher<OAuthClient>();
		SeedActiveClient(db, "c2", "right", hasher);
		var sut = new OAuthClientValidator(db, hasher, NullLogger<OAuthClientValidator>.Instance);

		(await sut.ValidateAsync("c2", "wrong")).Should().BeFalse();
	}
}
