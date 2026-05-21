using System.Globalization;
using System.Resources;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Localization.Mobile;
using BeDemo.Api.Localization.Portal;

namespace BeDemo.Api.Tests;

/// <summary>
/// Ensures <c>en</c>, <c>sk</c>, <c>cs</c>, <c>de</c>, <c>fr</c>, and <c>it</c> satellite files share identical key sets per app.
/// Complements <see cref="LocalizationPortalGoldenTests"/> which locks English JSON subtree values for portal auth flows.
/// </summary>
public class LocalizationKeyParityTests
{
    public static IEnumerable<object[]> AppResourceTypes() =>
    [
        [typeof(PortalResources), "Portal"],
        [typeof(AdminResources), "Admin"],
        [typeof(MobileResources), "Mobile"],
    ];

    [Theory]
    [MemberData(nameof(AppResourceTypes))]
    public void AllSupportedCultures_HaveSameKeySet(Type resourceType, string _)
    {
        var rm = new ResourceManager(resourceType.FullName!, resourceType.Assembly);
        var en = Keys(rm, CultureInfo.GetCultureInfo("en"));
        var sk = Keys(rm, CultureInfo.GetCultureInfo("sk"));
        var cs = Keys(rm, CultureInfo.GetCultureInfo("cs"));
        var de = Keys(rm, CultureInfo.GetCultureInfo("de"));
        var fr = Keys(rm, CultureInfo.GetCultureInfo("fr"));
        var it = Keys(rm, CultureInfo.GetCultureInfo("it"));
        Assert.Equal(en, sk);
        Assert.Equal(en, cs);
        Assert.Equal(en, de);
        Assert.Equal(en, fr);
        Assert.Equal(en, it);
    }

    private static HashSet<string> Keys(ResourceManager rm, CultureInfo culture)
    {
        var set = rm.GetResourceSet(culture, true, true)
            ?? throw new InvalidOperationException($"Missing resource set for {culture.Name}");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in set)
        {
            if (entry.Key is string k)
                keys.Add(k);
        }
        return keys;
    }
}
