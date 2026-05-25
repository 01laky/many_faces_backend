using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using BeDemo.Api.Localization;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Services;
using BeDemo.Api.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeDemo.Api.Tests;

/// <summary>Story detail management UI keys must exist in the admin static bundle (SDM §3.6).</summary>
public sealed class AdminStoryDetailLocalizationTests
{
	private static readonly string[] RequiredStoryDetailKeys =
	[
		"pages.storyDetail.createdAt",
		"pages.storyDetail.updatedAt",
		"pages.storyDetail.expiresAt",
		"pages.storyDetail.scheduledPublishAt",
		"pages.storyDetail.creatorLabel",
		"pages.storyDetail.facesLabel",
		"pages.storyDetail.facesUntargeted",
		"pages.storyDetail.stateDraft",
		"pages.storyDetail.statePublished",
		"pages.storyDetail.liveYes",
		"pages.storyDetail.liveNo",
		"pages.storyDetail.imagesSection",
		"pages.storyDetail.imagesEmpty",
		"pages.storyDetail.viewersSection",
		"pages.storyDetail.managementSection",
		"pages.storyDetail.openChat",
		"pages.storyDetail.deleteStory",
		"pages.storyDetail.deleteImage",
		"pages.storyDetail.successDeleteStory",
		"pages.storyDetail.imageDeleteBlockedLive",
		"pages.storiesTable.colImageCount",
		"pages.storiesTable.colExpiresAt",
		"pages.storiesTable.colCreator",
		"pages.storiesTable.filterAll",
		"pages.storiesTable.filterDraft",
		"pages.storiesTable.filterPublished",
		"pages.userDetail.storiesTitle",
		"pages.userDetail.storiesEmpty",
	];

	[Fact]
	public void AdminResx_ShouldContainStoryDetailManagementKeys()
	{
		var rm = new ResourceManager(typeof(AdminResources).FullName!, typeof(AdminResources).Assembly);
		foreach (var culture in new[] { "en", "sk", "cs" })
		{
			var ci = CultureInfo.GetCultureInfo(culture);
			foreach (var key in RequiredStoryDetailKeys)
			{
				rm.GetString(key, ci).Should().NotBeNullOrWhiteSpace($"missing {key} ({culture})");
			}
		}
	}

	[Fact]
	public void AdminBundle_ShouldExposeStoryDetailKeysInNestedJson()
	{
		var svc = new LocalizationBundleService(
			new MemoryCache(new MemoryCacheOptions()),
			new HostEnvironmentStub(),
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = svc.GetBundle(LocalizationApp.Admin);
		bundle.Should().NotBeNull();
		var common = bundle!.Resources["en"]["common"] as JsonObject;
		common.Should().NotBeNull();
		var storyDetail = common!["pages"]?["storyDetail"] as JsonObject;
		storyDetail.Should().NotBeNull();
		storyDetail!.ContainsKey("createdAt").Should().BeTrue();
		storyDetail.ContainsKey("managementSection").Should().BeTrue();
		storyDetail["createdAt"]!.GetValue<string>().Should().Be("Created at");

		var storiesTable = common["pages"]?["storiesTable"] as JsonObject;
		storiesTable.Should().NotBeNull();
		storiesTable!.ContainsKey("filterPublished").Should().BeTrue();
	}
}
