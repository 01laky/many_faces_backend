using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Moderation;
using BeDemo.Api.Services;
using FluentAssertions;

namespace BeDemo.Api.Tests.Services;

/// <summary>BE-RA19…RA24 — moderation queue DTO mapping (PI-8 preview fields).</summary>
[Trait(ContentModerationCiGate.XunitTraitName, ContentModerationCiGate.XunitTraitCategory)]
public sealed class ContentModerationQueueMapperTests
{
	private static ApplicationUser Creator(string id = "creator-1") =>
		new()
		{
			Id = id,
			FirstName = "Ada",
			LastName = "Lovelace",
			UserName = "ada@demo.com",
			Email = "ada@demo.com",
		};

	[Fact]
	public void BE_RA19_MapAlbum_SetsPlainTextPreviewAndNullMedia()
	{
		var album = new Album
		{
			Id = 10,
			Title = "Summer",
			Description = "<p>Beach <b>day</b></p>",
			CreatorId = "creator-1",
			Creator = Creator(),
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
			AiReviewStatus = AiReviewStatus.InProgress,
			CreatedAt = DateTime.UtcNow,
		};

		var dto = ContentModerationQueueMapper.MapAlbum(album, faceId: 3, faceTitle: "Public");

		dto.ContentType.Should().Be(ModeratedContentType.Album);
		dto.ContentId.Should().Be(10);
		dto.FaceId.Should().Be(3);
		dto.FaceTitle.Should().Be("Public");
		dto.CreatorName.Should().Be("Ada Lovelace");
		dto.BodyPreviewPlainText.Should().NotContain("<");
		dto.MediaUrlPreview.Should().BeNull();
	}

	[Fact]
	public void BE_RA20_MapBlog_UsesFaceTitleAndFirstImagePreview()
	{
		var blog = new Blog
		{
			Id = 20,
			Title = "Post",
			Content = "<script>x</script><p>Hi</p>",
			FaceId = 5,
			Face = new Face { Id = 5, Title = "Blog Face", Index = "bf", CreatedAt = DateTime.UtcNow },
			CreatorId = "creator-1",
			Creator = Creator(),
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
			AiReviewStatus = AiReviewStatus.Queued,
			CreatedAt = DateTime.UtcNow,
			Images =
			[
				new BlogImage { SortOrder = 2, ImageUrl = "https://cdn.example/second.jpg" },
				new BlogImage { SortOrder = 1, ImageUrl = "https://cdn.example/first.jpg" },
			],
		};

		var faceTitle = blog.Face!.Title;
		var firstImageUrl = blog.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault();
		var dto = ContentModerationQueueMapper.MapBlog(blog, faceTitle, firstImageUrl);

		dto.ContentType.Should().Be(ModeratedContentType.Blog);
		dto.FaceTitle.Should().Be("Blog Face");
		dto.BodyPreviewPlainText.Should().Contain("Hi").And.NotContain("script");
		dto.MediaUrlPreview.Should().Be("https://cdn.example/first.jpg");
	}

	[Fact]
	public void BE_RA21_MapReel_IncludesVideoPreview()
	{
		var reel = new Reel
		{
			Id = 30,
			Title = "Clip",
			Description = "Fun clip",
			VideoUrl = "https://cdn.example/clip.mp4",
			CreatorId = "creator-1",
			Creator = Creator(),
			ApprovalStatus = ContentApprovalStatus.Approved,
			AiReviewStatus = AiReviewStatus.RecommendedApprove,
			CreatedAt = DateTime.UtcNow,
		};

		var dto = ContentModerationQueueMapper.MapReel(reel, faceId: 7, faceTitle: "Reels");

		dto.ContentType.Should().Be(ModeratedContentType.Reel);
		dto.MediaUrlPreview.Should().Be("https://cdn.example/clip.mp4");
		dto.BodyPreviewPlainText.Should().Be("Fun clip");
	}

	[Theory]
	[InlineData("Ada", "Lovelace", "Ada Lovelace")]
	[InlineData(null, "Solo", "Solo")]
	[InlineData("Only", null, "Only")]
	[InlineData(null, null, "")]
	public void BE_RA22_CreatorDisplayName_TrimsAndHandlesNulls(
		string? first,
		string? last,
		string expected)
	{
		var user = new ApplicationUser { FirstName = first, LastName = last };
		ContentModerationQueueMapper.CreatorDisplayName(user).Should().Be(expected);
	}

	[Fact]
	public void BE_RA23_MapAlbum_CopiesAiReviewFields()
	{
		var album = new Album
		{
			Id = 1,
			Title = "T",
			CreatorId = "c",
			Creator = Creator("c"),
			ApprovalStatus = ContentApprovalStatus.Rejected,
			AiReviewStatus = AiReviewStatus.NeedsHumanReview,
			AiReviewDecision = AiReviewDecision.Reject,
			AiReviewConfidence = 0.91,
			AiReviewRiskLevel = AiReviewRiskLevel.High,
			AiReviewFlagsJson = "[\"spam\"]",
			AiReviewReason = "policy",
			AiReviewUserMessage = "try again",
			AiReviewModelVersion = "v1",
			AiReviewTraceId = "trace-1",
			SubmittedAtUtc = DateTime.UtcNow.AddHours(-2),
			HumanReviewedAtUtc = DateTime.UtcNow.AddHours(-1),
			HumanDecisionReason = "manual",
			RemovedAtUtc = null,
			RemovalReason = null,
			CreatedAt = DateTime.UtcNow.AddDays(-1),
		};

		var dto = ContentModerationQueueMapper.MapAlbum(album, 1, "F");

		dto.AiReviewDecision.Should().Be(AiReviewDecision.Reject);
		dto.AiReviewConfidence.Should().Be(0.91);
		dto.AiReviewTraceId.Should().Be("trace-1");
		dto.HumanDecisionReason.Should().Be("manual");
	}

	[Fact]
	public void BE_RA24_ModerationItemDto_IsPublicRecordInModelsNamespace()
	{
		typeof(ModerationItemDto).Namespace.Should().Be("BeDemo.Api.Models.DTOs.Moderation");
		typeof(ModerationItemDto).IsPublic.Should().BeTrue();
	}
}
