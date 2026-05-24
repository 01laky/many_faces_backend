using BeDemo.Api.Services;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Asserts BSH3 CI filter stays aligned with <see cref="BackendSecurityCiGate.XunitFilterExpression"/>.</summary>
[Trait("Category", "BackendSecurity")]
public sealed class BackendSecurityCiGateTests
{
    [Fact]
    public void XunitFilterExpression_matches_expected_trait()
    {
        BackendSecurityCiGate.XunitFilterExpression.Should().Be("Category=BackendSecurity");
    }
}
