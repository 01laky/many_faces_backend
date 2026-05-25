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

/// <summary>Settings infrastructure smoke panel keys must exist in the admin static bundle.</summary>
public sealed class AdminInfraLocalizationTests
{
    private static readonly string[] RequiredInfraKeys =
    [
        "pages.settings.infra.sectionTitle",
        "pages.settings.infra.mail.title",
        "pages.settings.infra.mail.send",
        "pages.settings.infra.mail.config.save",
        "pages.settings.infra.mail.config.testSmtp.action",
        "pages.settings.infra.mail.config.status.incomplete",
        "pages.settings.infra.status.incomplete",
        "pages.settings.infra.push.title",
        "pages.settings.infra.push.config.save",
        "pages.settings.infra.push.config.platform.sectionTitle",
        "pages.settings.infra.push.config.testFcm.action",
        "pages.settings.infra.push.config.status.incomplete",
        "pages.settings.infra.search.refresh",
        "pages.settings.infra.status.notConfigured",
        "pages.settings.infra.links.mailpit",
        "pages.settings.infra.errors.generic",
    ];

    [Fact]
    public void AdminResx_ShouldContainInfraSmokePanelKeys()
    {
        var rm = new ResourceManager(typeof(AdminResources).FullName!, typeof(AdminResources).Assembly);
        foreach (var culture in new[] { "en", "sk", "cs" })
        {
            var ci = CultureInfo.GetCultureInfo(culture);
            foreach (var key in RequiredInfraKeys)
            {
                rm.GetString(key, ci).Should().NotBeNullOrWhiteSpace($"missing {key} ({culture})");
            }
        }
    }

    [Fact]
    public void AdminBundle_ShouldExposeInfraKeysInNestedJson()
    {
        var svc = new LocalizationBundleService(
            new MemoryCache(new MemoryCacheOptions()),
            new HostEnvironmentStub(),
            NullLogger<LocalizationBundleService>.Instance);
        var bundle = svc.GetBundle(LocalizationApp.Admin);
        bundle.Should().NotBeNull();
        var infra = bundle!.Resources["en"]["common"]["pages"]?["settings"]?["infra"] as JsonObject;
        infra.Should().NotBeNull();
        infra!.ContainsKey("sectionTitle").Should().BeTrue();
        infra["sectionTitle"]!.GetValue<string>().Should().Be("Infrastructure & workers");
        var mail = infra["mail"] as JsonObject;
        mail.Should().NotBeNull();
        mail!.ContainsKey("title").Should().BeTrue();
        var mailConfig = mail["config"] as JsonObject;
        mailConfig.Should().NotBeNull();
        mailConfig!.ContainsKey("save").Should().BeTrue();
        (mailConfig["testSmtp"] as JsonObject).Should().NotBeNull();
        mailConfig["testSmtp"]!.AsObject().ContainsKey("action").Should().BeTrue();

        var push = infra["push"] as JsonObject;
        push.Should().NotBeNull();
        push!.ContainsKey("title").Should().BeTrue();
        var pushConfig = push["config"] as JsonObject;
        pushConfig.Should().NotBeNull();
        pushConfig!.ContainsKey("save").Should().BeTrue();
        (pushConfig["platform"] as JsonObject).Should().NotBeNull();
        pushConfig["platform"]!.AsObject().ContainsKey("sectionTitle").Should().BeTrue();
        (pushConfig["testFcm"] as JsonObject).Should().NotBeNull();
        pushConfig["testFcm"]!.AsObject().ContainsKey("action").Should().BeTrue();
    }
}
