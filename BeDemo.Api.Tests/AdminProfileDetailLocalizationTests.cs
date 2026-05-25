using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using BeDemo.Api.Localization;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using BeDemo.Api.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeDemo.Api.Tests;

/// <summary>Face profile detail Template B keys must exist in the admin static bundle (§4.4).</summary>
public sealed class AdminProfileDetailLocalizationTests
{
	private static readonly string[] RequiredProfileDetailKeys =
	[
		"pages.profileDetail.title",
		"pages.profileDetail.displayName",
		"pages.profileDetail.userId",
		"pages.profileDetail.avatarSection",
		"pages.profileDetail.avatarEmpty",
		"pages.profileDetail.avatarThumb",
		"pages.profileDetail.commentsSection",
		"pages.profileDetail.commentsEmpty",
		"pages.profileDetail.commentsSearchPlaceholder",
		"pages.profileDetail.reviewsSection",
		"pages.profileDetail.reviewsEmpty",
		"pages.profileDetail.reviewsSearchPlaceholder",
		"pages.profileDetail.managementSection",
		"pages.profileDetail.openChat",
		"pages.profileDetail.openUser",
		"pages.profileDetail.deleteCommentDialogTitle",
		"pages.profileDetail.deleteReviewDialogTitle",
		"pages.profileDetail.successDeleteComment",
		"pages.profileDetail.successDeleteReview",
		"pages.profileDetail.colCommentId",
		"pages.profileDetail.colAuthor",
		"pages.profileDetail.colBody",
		"pages.profileDetail.colCreatedAt",
		"pages.profileDetail.colReviewId",
		"pages.profileDetail.colTitle",
		"pages.profileDetail.colStars",
		"pages.profileDetail.colText",
		"pages.profileDetail.colReviewCreatedAt",
		"pages.profileDetail.nickname",
		"pages.profileDetail.age",
		"pages.profileDetail.rod",
		"pages.profileDetail.faceLink",
		"pages.profileDetail.faceVisibility",
		"pages.profileDetail.faceRoleName",
		"pages.profileDetail.isActive",
		"pages.profileDetail.visited",
		"pages.profileDetail.isFaceBanned",
		"pages.profileDetail.faceBannedBadge",
		"pages.profileDetail.commentsCount",
		"pages.profileDetail.likesCount",
		"pages.profileDetail.reviewsCount",
		"pages.profileDetail.faceAllowsRecensions",
		"pages.profileDetail.createdAt",
		"pages.profileDetail.updatedAt",
		"pages.profilesTable.colComments",
		"pages.profilesTable.colLikes",
		"pages.profilesTable.colReviews",
	];

	[Theory]
	[InlineData("en")]
	[InlineData("sk")]
	[InlineData("cs")]
	public void AdminResx_ShouldContainProfileDetailKeys(string culture)
	{
		var rm = new ResourceManager(typeof(AdminResources).FullName!, typeof(AdminResources).Assembly);
		var ci = CultureInfo.GetCultureInfo(culture);
		foreach (var key in RequiredProfileDetailKeys)
		{
			rm.GetString(key, ci).Should().NotBeNullOrWhiteSpace($"missing {key} for {culture}");
		}
	}

	[Fact]
	public void AdminBundle_ShouldExposeProfileDetailKeysInNestedJson()
	{
		var svc = new LocalizationBundleService(
			new MemoryCache(new MemoryCacheOptions()),
			new HostEnvironmentStub(),
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = svc.GetBundle(LocalizationApp.Admin);
		bundle.Should().NotBeNull();
		var common = bundle!.Resources["en"]["common"] as JsonObject;
		common.Should().NotBeNull();
		var profileDetail = common!["pages"]?["profileDetail"] as JsonObject;
		profileDetail.Should().NotBeNull();
		profileDetail!.ContainsKey("commentsSection").Should().BeTrue();
		profileDetail.ContainsKey("managementSection").Should().BeTrue();
		profileDetail.ContainsKey("deleteCommentDialogTitle").Should().BeTrue();

		var profilesTable = common["pages"]?["profilesTable"] as JsonObject;
		profilesTable.Should().NotBeNull();
		profilesTable!.ContainsKey("colComments").Should().BeTrue();
		profilesTable.ContainsKey("colLikes").Should().BeTrue();
		profilesTable.ContainsKey("colReviews").Should().BeTrue();
	}
}
