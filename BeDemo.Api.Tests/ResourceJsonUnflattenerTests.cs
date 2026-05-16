using System.Text.Json.Nodes;
using BeDemo.Api.Localization;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for <see cref="ResourceJsonUnflattener"/> nesting rules (§4.2 centralized-static-i18n prompt).
/// </summary>
/// <remarks>
/// Ambiguous prefix detection for real <c>.resx</c> files lives in <see cref="ResxLocalizationKeyAmbiguityTests"/>.
/// </remarks>
public class ResourceJsonUnflattenerTests
{
    [Fact]
    public void ToNestedObject_SingleSegment()
    {
        var obj = ResourceJsonUnflattener.ToNestedObject(new Dictionary<string, string> { ["welcome"] = "Hi" });
        Assert.Equal("Hi", obj["welcome"]?.GetValue<string>());
    }

    [Fact]
    public void ToNestedObject_DeepNesting()
    {
        var obj = ResourceJsonUnflattener.ToNestedObject(new Dictionary<string, string>
        {
            ["pages.login.title"] = "Login",
        });
        Assert.Equal("Login", obj["pages"]?["login"]?["title"]?.GetValue<string>());
    }

    /// <summary>
    /// §4.2 requires at least three nesting levels (e.g. <c>pages.section.field.label</c>).
    /// </summary>
    [Fact]
    public void ToNestedObject_DeepNesting_FourLevels()
    {
        var obj = ResourceJsonUnflattener.ToNestedObject(new Dictionary<string, string>
        {
            ["pages.register.validation.passwordMinLength"] = "Password must be at least 4 characters",
        });

        Assert.Equal(
            "Password must be at least 4 characters",
            obj["pages"]?["register"]?["validation"]?["passwordMinLength"]?.GetValue<string>());
    }

    [Fact]
    public void FindAmbiguousFlatKeys_ReturnsEmpty_ForCompatibleSet()
    {
        var keys = new[] { "welcome", "pages.login.title", "pages.register.title" };
        Assert.Empty(ResourceJsonUnflattener.FindAmbiguousFlatKeys(keys));
    }

    [Fact]
    public void ToNestedObject_AmbiguousBranchThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ResourceJsonUnflattener.ToNestedObject(new Dictionary<string, string>
            {
                ["pages.login"] = "x",
                ["pages.login.title"] = "Login",
            }));
    }

    [Fact]
    public void ToMobileNamespaces_SplitsByPrefix()
    {
        var ns = ResourceJsonUnflattener.ToMobileNamespaces(new Dictionary<string, string>
        {
            ["common.authRetryTitle"] = "Retry",
            ["register.title"] = "Register",
        });
        Assert.Equal("Retry", ns["common"]["authRetryTitle"]?.GetValue<string>());
        Assert.Equal("Register", ns["register"]["title"]?.GetValue<string>());
    }
}
