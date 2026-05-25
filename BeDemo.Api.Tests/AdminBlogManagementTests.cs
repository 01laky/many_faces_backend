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

/// <summary>Blog operator hard-delete, image delete, reject DM, queue contentId (admin blog detail prompt §10).</summary>
public sealed class AdminBlogManagementTests
	: IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
	private readonly RegistrationInviteWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public AdminBlogManagementTests(RegistrationInviteWebApplicationFactory factory)
	{
		_factory = factory;
		_client = factory.CreateFaceClient("public");
	}

	public void Dispose() => _client.Dispose();

	private async Task<(int BlogId, int FaceId, string CreatorId)> SeedBlogAsync(bool withImages = true)
	{
		var email = $"blog_mgmt_{Guid.NewGuid():N}@test.com";
		var tokens = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			"Test1234!@##",
			"Blog",
			"Tester");
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(
			_client,
			tokens.AccessToken,
			"public");

		var imageUrls = withImages
			? new[] { "https://picsum.photos/seed/blogtest/640/400" }
			: Array.Empty<string>();

		var create = await _client.PostAsJsonAsync("/api/blogs", new
		{
			title = $"Mgmt Blog {Guid.NewGuid():N}",
			content = "<p>Test <strong>HTML</strong> body</p>",
			faceId,
			imageUrls,
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var blogId = created.GetProperty("id").GetInt32();
		var creatorId = created.GetProperty("creatorId").GetString()!;
		return (blogId, faceId, creatorId);
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
	public async Task GetBlogDetail_ShouldReturnContentPlainText_AndAiFields()
	{
		var (blogId, faceId, _) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();
		var detail = await super.GetFromJsonAsync<JsonElement>($"/api/blogs/{blogId}?faceId={faceId}");
		detail.GetProperty("approvalStatus").GetString().Should().Be(nameof(ContentApprovalStatus.PendingApproval));
		detail.GetProperty("contentPlainText").GetString().Should().Contain("HTML");
		detail.TryGetProperty("aiReviewDecision", out _).Should().BeTrue();
	}

	[Fact]
	public async Task ListBlogs_ByCreatorId_ShouldReturnOnlyThatCreator()
	{
		var (blogId, faceId, creatorId) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();
		var list = await super.GetFromJsonAsync<JsonElement>($"/api/blogs?creatorId={creatorId}&page=1&pageSize=50");
		var items = list.GetProperty("items").EnumerateArray().ToList();
		items.Should().Contain(i => i.GetProperty("id").GetInt32() == blogId);
		items.Should().OnlyContain(i => i.GetProperty("creatorId").GetString() == creatorId);
		_ = faceId;
	}

	[Fact]
	public async Task BlogListQuery_WithoutFaceOrCreator_ShouldReturn400()
	{
		using var super = await CreateSuperAdminClientAsync();
		var res = await super.GetAsync("/api/blogs?page=1&pageSize=10");
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task HardDeleteBlog_ShouldRemoveRow_AndSendDm()
	{
		var (blogId, faceId, creatorId) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();

		var first = await super.PostAsJsonAsync(
			$"/api/operator-content/blogs/{blogId}/delete",
			DeleteBody(faceId, "1"));
		first.StatusCode.Should().Be(HttpStatusCode.NoContent);

		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			(await db.Blogs.AnyAsync(b => b.Id == blogId)).Should().BeFalse();
			var dm = await db.Messages
				.Where(m => m.IsPlatformDirectMessage && m.ReceiverId == creatorId)
				.OrderByDescending(m => m.Id)
				.FirstAsync();
			dm.Content.Should().Contain("Creator message long enough 1");
		}
	}

	[Fact]
	public async Task DeleteBlogImage_ShouldRemoveImageOnly()
	{
		var (blogId, faceId, _) = await SeedBlogAsync(withImages: true);
		using var super = await CreateSuperAdminClientAsync();
		var detail = await super.GetFromJsonAsync<JsonElement>($"/api/blogs/{blogId}?faceId={faceId}");
		var imageId = detail.GetProperty("images").EnumerateArray().First().GetProperty("id").GetInt32();

		var del = await super.PostAsJsonAsync(
			$"/api/operator-content/blogs/{blogId}/images/{imageId}/delete",
			DeleteBody(faceId, "img"));
		del.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var after = await super.GetFromJsonAsync<JsonElement>($"/api/blogs/{blogId}?faceId={faceId}");
		after.GetProperty("imageCount").GetInt32().Should().Be(0);
		after.GetProperty("id").GetInt32().Should().Be(blogId);
	}

	[Fact]
	public async Task RemoveViaModeration_ShouldHardDeleteBlog()
	{
		var (blogId, faceId, _) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();

		var remove = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Blog/{blogId}/remove",
			DeleteBody(faceId, "rm"));
		remove.StatusCode.Should().Be(HttpStatusCode.OK);

		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		(await db.Blogs.AnyAsync(b => b.Id == blogId)).Should().BeFalse();
	}

	[Fact]
	public async Task Reject_ShouldRequireUserMessage_AndSendPlatformDm()
	{
		var (blogId, _, creatorId) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();
		var beforeCount = await CountPlatformDmsAsync(creatorId);

		var rejectMissing = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Blog/{blogId}/reject",
			new { reason = "Policy mismatch on blog content only" });
		rejectMissing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var reject = await super.PostAsJsonAsync(
			$"/api/contentmoderation/Blog/{blogId}/reject",
			new
			{
				reason = "Policy mismatch on blog content",
				userMessage = "Please update your blog per our guidelines.",
			});
		reject.StatusCode.Should().Be(HttpStatusCode.OK);
		(await CountPlatformDmsAsync(creatorId)).Should().BeGreaterThan(beforeCount);
	}

	[Fact]
	public async Task ModerationQueue_ContentIdFilter_ShouldReturnSingleBlog()
	{
		var (blogId, faceId, _) = await SeedBlogAsync();
		using var super = await CreateSuperAdminClientAsync();

		var queue = await super.GetFromJsonAsync<JsonElement>(
			$"/api/contentmoderation?contentType=Blog&contentId={blogId}&faceId={faceId}&page=1&pageSize=50");
		var items = queue.GetProperty("items").EnumerateArray().ToList();
		items.Should().HaveCount(1);
		items[0].GetProperty("contentId").GetInt32().Should().Be(blogId);
	}

	[Fact]
	public async Task HardDeleteBlog_WhenDmFails_ShouldStillDelete()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"blog-dm-fail-{Guid.NewGuid():N}")
			.Options;
		await using var db = new ApplicationDbContext(options);
		db.Database.EnsureCreated();
		await SeedUsersForDmAsync(db);

		var blog = new Blog
		{
			Title = "DM Fail Blog",
			CreatorId = "u1",
			FaceId = 1,
			Content = "body",
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
		};
		db.Blogs.Add(blog);
		await db.SaveChangesAsync();

		var failingDm = new Mock<IPlatformDirectMessageService>();
		failingDm
			.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("simulated messenger outage"));

		var svc = new OperatorBlogManagementService(db, failingDm.Object, NullLogger<OperatorBlogManagementService>.Instance);
		var ok = await svc.HardDeleteBlogAsync(
			"s1",
			blog.Id,
			1,
			"Audit reason long enough",
			"Creator message long enough");
		ok.Should().BeTrue();
		(await db.Blogs.AnyAsync(b => b.Id == blog.Id)).Should().BeFalse();
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
