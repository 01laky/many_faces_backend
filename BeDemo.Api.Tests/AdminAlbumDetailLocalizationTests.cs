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

/// <summary>Album detail management UI keys must exist in the admin static bundle (§3.6).</summary>
public sealed class AdminAlbumDetailLocalizationTests
{
	private static readonly string[] RequiredAlbumDetailKeys =
	[
		"pages.albumDetail.approve",
		"pages.albumDetail.mediaSection",
		"pages.albumDetail.openChat",
		"pages.albumDetail.deleteAlbum",
		"pages.albumDetail.reasonLabel",
		"pages.albumsTable.mediaCount",
	];

	[Fact]
	public void AdminResx_ShouldContainAlbumDetailManagementKeys()
	{
		var rm = new ResourceManager(typeof(AdminResources).FullName!, typeof(AdminResources).Assembly);
		foreach (var key in RequiredAlbumDetailKeys)
		{
			rm.GetString(key, CultureInfo.GetCultureInfo("en")).Should().NotBeNullOrWhiteSpace($"missing {key}");
		}
	}

	[Fact]
	public void AdminBundle_ShouldExposeAlbumDetailKeysInNestedJson()
	{
		var env = new HostEnvironmentStub();
		var svc = new LocalizationBundleService(
			new MemoryCache(new MemoryCacheOptions()),
			env,
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = svc.GetBundle(LocalizationApp.Admin);
		bundle.Should().NotBeNull();
		var common = bundle!.Resources["en"]["common"] as JsonObject;
		common.Should().NotBeNull();
		var albumDetail = common!["pages"]?["albumDetail"] as JsonObject;
		albumDetail.Should().NotBeNull();
		albumDetail!.ContainsKey("approve").Should().BeTrue();
		albumDetail.ContainsKey("mediaSection").Should().BeTrue();
	}

	[Fact]
	public void AdminBundle_UserChatCharCount_ShouldUseI18nextInterpolationMarkers()
	{
		var svc = new LocalizationBundleService(
			new MemoryCache(new MemoryCacheOptions()),
			new HostEnvironmentStub(),
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = svc.GetBundle(LocalizationApp.Admin);
		var charCount = bundle!.Resources["en"]["common"]?["pages"]?["userChat"]?["charCount"]?.GetValue<string>();
		Assert.Equal("{{count}} / {{max}} characters", charCount);
	}
}
