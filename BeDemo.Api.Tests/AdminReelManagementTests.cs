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

/// <summary>Reel operator hard-delete, reject DM, queue contentId filter (admin reel detail prompt §10).</summary>
public sealed class AdminReelManagementTests
	: IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
	private readonly RegistrationInviteWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public AdminReelManagementTests(RegistrationInviteWebApplicationFactory factory)
	{
		_factory = factory;
		_client = factory.CreateFaceClient("public");
	}

	public void Dispose() => _client.Dispose();

	private async Task<(string UserToken, int ReelId, int FaceId, string CreatorId)> SeedReelAsync()
	{
		var email = $"reel_mgmt_{Guid.NewGuid():N}@test.com";
		var tokens = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			"Test1234!@##",
			"Reel",
			"Tester");
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(
			_client,
			tokens.AccessToken,
			"public");

		var create = await _client.PostAsJsonAsync("/api/reels", new
		{
			title = $"Mgmt Reel {Guid.NewGuid():N}",
			description = "Test",
			videoUrl = "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
			faceIds = new[] { faceId },
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var reelId = created.GetProperty("id").GetInt32();
		var creatorId = created.GetProperty("creatorId").GetString()!;
		return (tokens.AccessToken, reelId, faceId, creatorId);
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
	public async Task GetReelDetail_ShouldReturnAiFields_ForOperator()
	{
		var (_, reelId, faceId, _) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();
		var detail = await super.GetFromJsonAsync<JsonElement>($"/api/reels/{reelId}?faceId={faceId}");
		detail.GetProperty("approvalStatus").GetString().Should().Be(nameof(ContentApprovalStatus.PendingApproval));
		detail.TryGetProperty("aiReviewDecision", out _).Should().BeTrue();
		detail.TryGetProperty("aiReviewRiskLevel", out _).Should().BeTrue();
	}

	[Fact]
	public async Task ListReels_ByCreatorId_ShouldReturnOnlyThatCreator()
	{
		var (_, reelId, faceId, creatorId) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();
		var list = await super.GetFromJsonAsync<JsonElement>($"/api/reels?creatorId={creatorId}&page=1&pageSize=50");
		var items = list.GetProperty("items").EnumerateArray().ToList();
		items.Should().Contain(i => i.GetProperty("id").GetInt32() == reelId);
		items.Should().OnlyContain(i => i.GetProperty("creatorId").GetString() == creatorId);
		_ = faceId;
	}

	[Fact]
	public async Task HardDeleteReel_ShouldPersistPlatformDm_AndBeIdempotent()
	{
		var (_, reelId, faceId, creatorId) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();

		var first = await super.PostAsJsonAsync(
			$"/api/operator-content/reels/{reelId}/delete",
			DeleteBody(faceId, "1"));
		first.StatusCode.Should().Be(HttpStatusCode.NoContent);

		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			(await db.Reels.AnyAsync(r => r.Id == reelId)).Should().BeFalse();
			var dm = await db.Messages
				.Where(m => m.IsPlatformDirectMessage && m.ReceiverId == creatorId)
				.OrderByDescending(m => m.Id)
				.FirstAsync();
			dm.Content.Should().Contain("Creator message long enough 1");
		}

		var second = await super.PostAsJsonAsync(
			$"/api/operator-content/reels/{reelId}/delete",
			DeleteBody(faceId, "2"));
		second.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task RemoveViaModeration_ShouldHardDeleteReel()
	{
		var (_, reelId, faceId, creatorId) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();

		var remove = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Reel/{reelId}/remove",
			DeleteBody(faceId, "rm"));
		remove.StatusCode.Should().Be(HttpStatusCode.OK);

		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		(await db.Reels.AnyAsync(r => r.Id == reelId)).Should().BeFalse();
		_ = creatorId;
	}

	[Fact]
	public async Task Reject_ShouldRequireUserMessage_AndSendPlatformDm()
	{
		var (_, reelId, _, creatorId) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();
		var beforeCount = await CountPlatformDmsAsync(creatorId);

		var rejectMissing = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Reel/{reelId}/reject",
			new { reason = "Policy mismatch on reel content only" });
		rejectMissing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var reject = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Reel/{reelId}/reject",
			new
			{
				reason = "Policy mismatch on reel content",
				userMessage = "Please update your reel per our guidelines.",
			});
		reject.StatusCode.Should().Be(HttpStatusCode.OK);
		(await CountPlatformDmsAsync(creatorId)).Should().BeGreaterThan(beforeCount);
	}

	[Fact]
	public async Task ModerationQueue_ContentIdFilter_ShouldReturnSingleReel()
	{
		var (_, reelId, faceId, _) = await SeedReelAsync();
		using var super = await CreateSuperAdminClientAsync();

		var queue = await super.GetFromJsonAsync<JsonElement>(
			$"/api/contentmoderation?contentType=Reel&contentId={reelId}&faceId={faceId}&page=1&pageSize=50");
		var items = queue.GetProperty("items").EnumerateArray().ToList();
		items.Should().HaveCount(1);
		items[0].GetProperty("contentId").GetInt32().Should().Be(reelId);
	}

	[Fact]
	public async Task HardDeleteReel_WhenDmFails_ShouldStillDelete()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"reel-dm-fail-{Guid.NewGuid():N}")
			.Options;
		await using var db = new ApplicationDbContext(options);
		db.Database.EnsureCreated();
		await SeedUsersForDmAsync(db);

		var reel = new Reel
		{
			Title = "DM Fail Reel",
			CreatorId = "u1",
			VideoUrl = "https://example.com/v.mp4",
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
		};
		db.Reels.Add(reel);
		await db.SaveChangesAsync();
		db.ReelFaces.Add(new ReelFace { ReelId = reel.Id, FaceId = 1 });
		await db.SaveChangesAsync();

		var failingDm = new Mock<IPlatformDirectMessageService>();
		failingDm
			.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("simulated messenger outage"));

		var svc = new OperatorReelManagementService(db, failingDm.Object, NullLogger<OperatorReelManagementService>.Instance);
		var ok = await svc.HardDeleteReelAsync(
			"s1",
			reel.Id,
			1,
			"Audit reason long enough",
			"Creator message long enough");
		ok.Should().BeTrue();
		(await db.Reels.AnyAsync(r => r.Id == reel.Id)).Should().BeFalse();
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
