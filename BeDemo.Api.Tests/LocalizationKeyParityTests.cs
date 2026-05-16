using System.Globalization;
using System.Resources;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Localization.Mobile;
using BeDemo.Api.Localization.Portal;

namespace BeDemo.Api.Tests;

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
    public void EnSkCs_HaveSameKeySet(Type resourceType, string _)
    {
        var rm = new ResourceManager(resourceType.FullName!, resourceType.Assembly);
        var en = Keys(rm, CultureInfo.GetCultureInfo("en"));
        var sk = Keys(rm, CultureInfo.GetCultureInfo("sk"));
        var cs = Keys(rm, CultureInfo.GetCultureInfo("cs"));
        Assert.Equal(en, sk);
        Assert.Equal(en, cs);
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
