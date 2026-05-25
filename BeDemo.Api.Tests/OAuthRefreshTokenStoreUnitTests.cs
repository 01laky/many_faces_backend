using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit-level refresh store behavior (in-memory EF) complementing HTTP integration tests in <see cref="RefreshTokenEdgeCaseTests"/>.
/// </summary>
public sealed class OAuthRefreshTokenStoreUnitTests
{
	private static (ApplicationDbContext Db, OAuthRefreshTokenStore Store) CreateSut()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		var db = new ApplicationDbContext(options);
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:RefreshTokenDaysSession"] = "14",
				["Jwt:RefreshTokenDaysRememberMe"] = "90",
			})
			.Build();
		var store = new OAuthRefreshTokenStore(db, config, NullLogger<OAuthRefreshTokenStore>.Instance);
		return (db, store);
	}

	[Fact]
	public async Task RedeemAndRotateAsync_ShouldReturnNull_WhenTokenUnknown()
	{
		var (db, store) = CreateSut();
		await using (db)
		{
			var result = await store.RedeemAndRotateAsync("unknown-plaintext");
			result.Should().BeNull();
		}
	}

	[Fact]
	public async Task RedeemAndRotateAsync_ShouldRotate_AndInvalidateOldPlaintext()
	{
		var (db, store) = CreateSut();
		await using (db)
		{
			const string userId = "user-refresh-1";
			const string plain = "plain-refresh-abc";
			await store.CreateAsync(userId, plain, useRememberMeAccessLifetime: false);

			var first = await store.RedeemAndRotateAsync(plain);
			first.Should().NotBeNull();
			first!.UserId.Should().Be(userId);
			first.NewPlainRefreshToken.Should().NotBeNullOrEmpty();

			var replay = await store.RedeemAndRotateAsync(plain);
			replay.Should().BeNull();

			var second = await store.RedeemAndRotateAsync(first.NewPlainRefreshToken);
			second.Should().NotBeNull();
			second!.UserId.Should().Be(userId);
		}
	}

	[Fact]
	public async Task CreateAsync_ShouldUseLongerExpiry_WhenRememberMeLifetime()
	{
		var (db, store) = CreateSut();
		await using (db)
		{
			await store.CreateAsync("u1", "tok-remember", useRememberMeAccessLifetime: true);
			var row = await db.OAuthRefreshTokens.SingleAsync();
			row.UseRememberMeAccessLifetime.Should().BeTrue();
			(row.ExpiresAtUtc - row.CreatedAtUtc).TotalDays.Should().BeApproximately(90, 0.5);
		}
	}

	[Fact]
	public async Task RevokeAllActiveForUserAsync_ShouldMarkActiveRowsRevoked()
	{
		var (db, store) = CreateSut();
		await using (db)
		{
			await store.CreateAsync("u2", "tok-a", false);
			await store.CreateAsync("u2", "tok-b", false);

			await store.RevokeAllActiveForUserAsync("u2");

			var active = await db.OAuthRefreshTokens.Where(t => t.UserId == "u2" && t.RevokedAtUtc == null).CountAsync();
			active.Should().Be(0);
		}
	}

	[Fact]
	public async Task RedeemAndRotateAsync_ShouldReturnNull_WhenTokenExpired()
	{
		var (db, store) = CreateSut();
		await using (db)
		{
			const string plain = "expired-plain";
			db.OAuthRefreshTokens.Add(new OAuthRefreshToken
			{
				TokenHash = TokenHasher.Sha256Hex(plain),
				UserId = "u3",
				CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
				ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
				UseRememberMeAccessLifetime = false,
			});
			await db.SaveChangesAsync();

			var result = await store.RedeemAndRotateAsync(plain);
			result.Should().BeNull();
		}
	}
}
