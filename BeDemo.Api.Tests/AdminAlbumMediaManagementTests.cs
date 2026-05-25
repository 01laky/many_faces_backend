using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Album media grid, operator hard-delete, and best-effort platform DM (prompt §9.3).</summary>
public sealed class AdminAlbumMediaManagementTests
	: IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
	private readonly RegistrationInviteWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public AdminAlbumMediaManagementTests(RegistrationInviteWebApplicationFactory factory)
	{
		_factory = factory;
		_client = factory.CreateFaceClient("public");
	}

	public void Dispose() => _client.Dispose();

	private async Task<(string UserToken, int AlbumId, int FaceId, string CreatorId)> SeedAlbumWithMediaAsync()
	{
		var email = $"album_media_{Guid.NewGuid():N}@test.com";
		var tokens = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			"Test1234!@##",
			"Media",
			"Tester");
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

		var create = await _client.PostAsJsonAsync("/api/albums", new
		{
			title = $"Media Album {Guid.NewGuid():N}",
			description = "Test",
			albumType = 1,
			mediaType = 1,
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var albumId = created.GetProperty("id").GetInt32();
		var creatorId = created.GetProperty("creatorId").GetString()!;

		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		db.AlbumMedia.AddRange(
			new AlbumMedia
			{
				AlbumId = albumId,
				MediaType = MediaTypeEnum.Image,
				ImageUrl = "https://cdn.example.com/photo.jpg",
				SortOrder = 0,
				Title = "Photo 1",
			},
			new AlbumMedia
			{
				AlbumId = albumId,
				MediaType = MediaTypeEnum.Video,
				ImageUrl = "https://cdn.example.com/poster.jpg",
				VideoUrl = "https://cdn.example.com/clip.mp4",
				SortOrder = 1,
				Title = "Clip 1",
			});
		await db.SaveChangesAsync();

		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(
			_client,
			tokens.AccessToken,
			"public");
		return (tokens.AccessToken, albumId, faceId, creatorId);
	}

	private async Task<HttpClient> CreateSuperAdminClientAsync()
	{
		using var admin = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
		var client = _factory.CreateFaceClient("admin");
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client;
	}

	private static object DeleteBody(int faceId, string suffix = "") => new
	{
		faceId,
		reason = $"Audit reason long enough {suffix}",
		userMessage = $"Creator message long enough {suffix}",
	};

	[Fact]
	public async Task GetAlbumDetail_ShouldReturnMediaItems_AndListMediaCount()
	{
		var (_, albumId, faceId, _) = await SeedAlbumWithMediaAsync();
		var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/albums/{albumId}?faceId={faceId}");
		detail.GetProperty("mediaCount").GetInt32().Should().Be(2);
		var items = detail.GetProperty("mediaItems").EnumerateArray().ToList();
		items.Should().HaveCount(2);
		items.Should().Contain(i => i.GetProperty("mediaType").GetString() == "Image");
		items.Should().Contain(i => i.GetProperty("mediaType").GetString() == "Video");

		using var super = await CreateSuperAdminClientAsync();
		var list = await super.GetFromJsonAsync<JsonElement>($"/api/albums?faceId={faceId}&page=1&pageSize=50");
		var row = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("id").GetInt32() == albumId);
		row.GetProperty("mediaCount").GetInt32().Should().Be(2);
	}

	[Fact]
	public async Task DeleteMedia_Should403_ForNonSuperAdmin_And204_ForSuperAdmin()
	{
		var (_, albumId, faceId, _) = await SeedAlbumWithMediaAsync();
		var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/albums/{albumId}?faceId={faceId}");
		var mediaId = detail.GetProperty("mediaItems")[0].GetProperty("id").GetInt32();

		var forbidden = await _client.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/media/{mediaId}/delete",
			DeleteBody(faceId));
		forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

		using var super = await CreateSuperAdminClientAsync();
		var ok = await super.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/media/{mediaId}/delete",
			DeleteBody(faceId));
		ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		(await db.AlbumMedia.AnyAsync(m => m.Id == mediaId)).Should().BeFalse();
	}

	[Fact]
	public async Task HardDeleteAlbum_ShouldPersistPlatformDm_AndBeIdempotent()
	{
		var (_, albumId, faceId, creatorId) = await SeedAlbumWithMediaAsync();
		using var super = await CreateSuperAdminClientAsync();

		var first = await super.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/delete",
			DeleteBody(faceId, "1"));
		first.StatusCode.Should().Be(HttpStatusCode.NoContent);

		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			(await db.Albums.AnyAsync(a => a.Id == albumId)).Should().BeFalse();
			var dm = await db.Messages
				.Where(m => m.IsPlatformDirectMessage && m.ReceiverId == creatorId)
				.OrderByDescending(m => m.Id)
				.FirstAsync();
			dm.Content.Should().Contain("Creator message long enough 1");
		}

		var second = await super.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/delete",
			DeleteBody(faceId, "2"));
		second.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task HardDeleteAlbum_WrongFaceId_Should404_OnMediaDelete()
	{
		var (_, albumId, faceId, _) = await SeedAlbumWithMediaAsync();
		using var super = await CreateSuperAdminClientAsync();
		var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/albums/{albumId}?faceId={faceId}");
		var mediaId = detail.GetProperty("mediaItems")[0].GetProperty("id").GetInt32();

		var res = await super.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/media/{mediaId}/delete",
			DeleteBody(faceId + 99999));
		res.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Reject_ShouldRequireUserMessage_AndApprove_ShouldNotCreatePlatformDm()
	{
		var (_, albumId, faceId, creatorId) = await SeedAlbumWithMediaAsync();
		using var super = await CreateSuperAdminClientAsync();

		var beforeCount = await CountPlatformDmsAsync(creatorId);

		var approve = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Album/{albumId}/approve",
			new { reason = "Approved from integration test" });
		approve.StatusCode.Should().Be(HttpStatusCode.OK);
		(await CountPlatformDmsAsync(creatorId)).Should().Be(beforeCount);

		var rejectMissingMessage = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Album/{albumId}/reject",
			new { reason = "Policy mismatch on album content only" });
		rejectMissingMessage.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var reject = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Album/{albumId}/reject",
			new
			{
				reason = "Policy mismatch on album content",
				userMessage = "Please update your album per our guidelines.",
			});
		reject.StatusCode.Should().Be(HttpStatusCode.OK);
		(await CountPlatformDmsAsync(creatorId)).Should().BeGreaterThan(beforeCount);
	}

	[Fact]
	public async Task OperatorAlbumDeleteRequest_Should400_WhenFieldsTooShort()
	{
		var (_, albumId, faceId, _) = await SeedAlbumWithMediaAsync();
		using var super = await CreateSuperAdminClientAsync();
		var bad = await super.PostAsJsonAsync(
			$"/api/operator-content/albums/{albumId}/delete",
			new { faceId, reason = "short", userMessage = "short" });
		bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task HardDeleteAlbum_WhenDmFails_ShouldStillDelete()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"album-dm-fail-{Guid.NewGuid():N}")
			.Options;
		await using var db = new ApplicationDbContext(options);
		db.Database.EnsureCreated();
		await SeedUsersForDmAsync(db);

		var album = new Album
		{
			Title = "DM Fail Album",
			CreatorId = "u1",
			AlbumType = AlbumTypeEnum.Public,
			MediaType = MediaTypeEnum.Image,
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
		};
		db.Albums.Add(album);
		await db.SaveChangesAsync();
		db.AlbumFaces.Add(new AlbumFace { AlbumId = album.Id, FaceId = 1 });
		await db.SaveChangesAsync();

		var failingDm = new Mock<IPlatformDirectMessageService>();
		failingDm
			.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("simulated messenger outage"));

		var svc = new OperatorAlbumManagementService(db, failingDm.Object, NullLogger<OperatorAlbumManagementService>.Instance);
		var ok = await svc.HardDeleteAlbumAsync(
			"s1",
			album.Id,
			1,
			"Audit reason long enough",
			"Creator message long enough");
		ok.Should().BeTrue();
		(await db.Albums.AnyAsync(a => a.Id == album.Id)).Should().BeFalse();
	}

	private async Task<int> CountPlatformDmsAsync(string creatorId)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		return await db.Messages.CountAsync(m =>
			m.IsPlatformDirectMessage && m.ReceiverId == creatorId);
	}

	private static async Task SeedUsersForDmAsync(ApplicationDbContext db)
	{
		var superRole = new UserRole { Id = 1, Name = UserRole.GlobalRoleNames.SuperAdmin };
		var userRole = new UserRole { Id = 2, Name = UserRole.GlobalRoleNames.User };
		db.UserRoles.AddRange(superRole, userRole);
		db.Users.Add(new ApplicationUser
		{
			Id = "s1",
			UserName = "s1@test",
			Email = "s1@test",
			UserRoleId = superRole.Id,
		});
		db.Users.Add(new ApplicationUser
		{
			Id = "u1",
			UserName = "u1@test",
			Email = "u1@test",
			UserRoleId = userRole.Id,
		});
		await db.SaveChangesAsync();
	}
}
