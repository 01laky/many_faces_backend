using System.Globalization;
using System.Resources;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Localization.Mobile;
using BeDemo.Api.Localization.Portal;

namespace BeDemo.Api.Tests;

/// <summary>
/// Guards static UI resource quality: no empty translated values and i18next placeholders survive .resx XML.
/// </summary>
public class LocalizationResourceValueTests
{
    public static IEnumerable<object[]> AppResourceTypes() =>
    [
        [typeof(PortalResources), "Portal"],
        [typeof(AdminResources), "Admin"],
        [typeof(MobileResources), "Mobile"],
    ];

    [Theory]
    [MemberData(nameof(AppResourceTypes))]
    public void EnSkCs_ValuesAreNotEmptyOrWhitespace(Type resourceType, string _)
    {
        var rm = new ResourceManager(resourceType.FullName!, resourceType.Assembly);
        foreach (var culture in new[] { "en", "sk", "cs" })
        {
            var ci = CultureInfo.GetCultureInfo(culture);
            var set = rm.GetResourceSet(ci, true, true)
                ?? throw new InvalidOperationException($"Missing resource set for {culture}");
            foreach (System.Collections.DictionaryEntry entry in set)
            {
                if (entry.Key is not string key || entry.Value is not string value)
                    continue;
                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{resourceType.Name} [{culture}] key '{key}' has empty value");
            }
        }
    }

    [Fact]
    public void MobileEn_PreservesI18nextInterpolationPlaceholders()
    {
        var rm = new ResourceManager(typeof(MobileResources).FullName!, typeof(MobileResources).Assembly);
        var set = rm.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true);
        Assert.NotNull(set);
        var welcome = set.Cast<System.Collections.DictionaryEntry>()
            .FirstOrDefault(e => e.Key is string k && k == "common.facePage.welcomeUser")
            .Value as string;
        Assert.NotNull(welcome);
        Assert.Contains("{{name}}", welcome, StringComparison.Ordinal);
    }
}
