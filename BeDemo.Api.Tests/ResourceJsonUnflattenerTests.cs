using System.Text.Json.Nodes;
using BeDemo.Api.Localization;

namespace BeDemo.Api.Tests;

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
