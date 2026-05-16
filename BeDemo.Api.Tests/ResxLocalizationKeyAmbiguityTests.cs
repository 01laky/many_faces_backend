using System.Resources;
using BeDemo.Api.Localization;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Localization.Mobile;
using BeDemo.Api.Localization.Portal;
using BeDemo.Api.Tests.Localization;

namespace BeDemo.Api.Tests;

/// <summary>
/// CI guard: embedded <c>.resx</c> files must not define ambiguous dotted keys (§4.2 centralized-static-i18n prompt).
/// </summary>
/// <remarks>
/// Conflicts such as <c>pages.login</c> + <c>pages.login.title</c> cannot be represented as nested JSON;
/// <see cref="ResourceJsonUnflattener.ToNestedObject"/> throws <see cref="InvalidOperationException"/> at runtime.
/// Scan all Portal/Admin/Mobile cultures (<c>en</c>, <c>sk</c>, <c>cs</c> satellites) so translators cannot
/// introduce prefix clashes in only one language file.
/// </remarks>
public class ResxLocalizationKeyAmbiguityTests
{
    public static IEnumerable<object[]> AppResourceTypes() =>
    [
        [typeof(PortalResources), "Portal"],
        [typeof(AdminResources), "Admin"],
        [typeof(MobileResources), "Mobile"],
    ];

    [Theory]
    [MemberData(nameof(AppResourceTypes))]
    public void EmbeddedResx_AllCultures_HaveNoAmbiguousKeyPrefixes(Type resourceType, string appLabel)
    {
        var rm = new ResourceManager(resourceType.FullName!, resourceType.Assembly);
        var allMessages = new List<string>();

        foreach (var culture in LocalizationResxKeyAmbiguityScanner.ResxCultures)
        {
            var conflicts = LocalizationResxKeyAmbiguityScanner.FindConflicts(rm, culture);
            var message = LocalizationResxKeyAmbiguityScanner.FormatConflictMessage(
                appLabel,
                culture.Name,
                conflicts);
            if (!string.IsNullOrEmpty(message))
                allMessages.Add(message);
        }

        Assert.True(allMessages.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, allMessages));
    }

    [Fact]
    public void FindAmbiguousFlatKeys_DetectsParentChildPair()
    {
        var conflicts = ResourceJsonUnflattener.FindAmbiguousFlatKeys(
        [
            "pages.login.title",
            "pages.login",
            "routes.home",
        ]);

        Assert.Single(conflicts);
        Assert.Equal(("pages.login", "pages.login.title"), conflicts[0]);
    }

    [Fact]
    public void FindAmbiguousFlatKeys_DetectsThreeLevelPrefix()
    {
        var conflicts = ResourceJsonUnflattener.FindAmbiguousFlatKeys(
        [
            "a.b.c.d",
            "a.b",
        ]);

        Assert.Contains(("a.b", "a.b.c.d"), conflicts);
    }

    [Fact]
    public void FindAmbiguousFlatKeys_AllowsSiblingKeysWithoutConflict()
    {
        var conflicts = ResourceJsonUnflattener.FindAmbiguousFlatKeys(
        [
            "pages.login.title",
            "pages.register.title",
            "routes.login",
        ]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void FindAmbiguousFlatKeys_AllowsSingleSegmentKeys()
    {
        var conflicts = ResourceJsonUnflattener.FindAmbiguousFlatKeys(["welcome", "save", "cancel"]);
        Assert.Empty(conflicts);
    }

    [Fact]
    public void ToNestedObject_MatchesFindAmbiguousFlatKeys_ForKnownBadSet()
    {
        var bad = new Dictionary<string, string>
        {
            ["pages.login"] = "leaf",
            ["pages.login.title"] = "Login",
        };

        Assert.NotEmpty(ResourceJsonUnflattener.FindAmbiguousFlatKeys(bad.Keys));
        Assert.Throws<InvalidOperationException>(() => ResourceJsonUnflattener.ToNestedObject(bad));
    }
}
