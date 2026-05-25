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

/// <summary>Dashboard redesign keys must exist in admin static bundle (metrics sections + charts).</summary>
public sealed class AdminDashboardLocalizationTests
{
	private static readonly string[] RequiredDashboardKeys =
	[
		"pages.dashboard.metrics.sectionTitle",
		"pages.dashboard.metrics.sectionLead",
		"pages.dashboard.metrics.sectionTotal",
		"pages.dashboard.metrics.chartEmpty",
		"pages.dashboard.metrics.sections.structure",
		"pages.dashboard.metrics.sections.structureDesc",
		"pages.dashboard.metrics.sections.social",
		"pages.dashboard.metrics.sections.socialDesc",
		"pages.dashboard.metrics.sections.messaging",
		"pages.dashboard.metrics.sections.messagingDesc",
		"pages.dashboard.metrics.sections.content",
		"pages.dashboard.metrics.sections.contentDesc",
		"pages.dashboard.metrics.sections.faceChat",
		"pages.dashboard.metrics.sections.faceChatDesc",
		"pages.dashboard.metrics.sections.profiles",
		"pages.dashboard.metrics.sections.profilesDesc",
		"pages.dashboard.metrics.sections.trust",
		"pages.dashboard.metrics.sections.trustDesc",
		"pages.dashboard.metrics.sections.wall",
		"pages.dashboard.metrics.sections.wallDesc",
		"pages.dashboard.metrics.wallTotal",
		"pages.dashboard.charts.sectionTitle",
		"pages.dashboard.charts.lineTitle",
		"pages.dashboard.charts.pieTitle",
		"pages.dashboard.charts.barTitle",
		"pages.dashboard.aiStats.title",
		"pages.dashboard.moderationWidget.title",
	];

	[Fact]
	public void AdminResx_ShouldContainDashboardRedesignKeys()
	{
		var rm = new ResourceManager(typeof(AdminResources).FullName!, typeof(AdminResources).Assembly);
		foreach (var culture in new[] { "en", "sk", "cs" })
		{
			var ci = CultureInfo.GetCultureInfo(culture);
			foreach (var key in RequiredDashboardKeys)
			{
				rm.GetString(key, ci).Should().NotBeNullOrWhiteSpace($"missing {key} ({culture})");
			}
		}
	}

	[Fact]
	public void AdminBundle_ShouldExposeMetricsSectionsInNestedJson()
	{
		var svc = new LocalizationBundleService(
			new MemoryCache(new MemoryCacheOptions()),
			new HostEnvironmentStub(),
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = svc.GetBundle(LocalizationApp.Admin);
		bundle.Should().NotBeNull();
		var metrics = bundle!.Resources["en"]["common"]?["pages"]?["dashboard"]?["metrics"] as JsonObject;
		metrics.Should().NotBeNull();
		metrics!.ContainsKey("sectionLead").Should().BeTrue();
		var sections = metrics["sections"] as JsonObject;
		sections.Should().NotBeNull();
		sections!.ContainsKey("structure").Should().BeTrue();
		sections.ContainsKey("wallDesc").Should().BeTrue();
		metrics.ContainsKey("wallTotal").Should().BeTrue();
	}
}
