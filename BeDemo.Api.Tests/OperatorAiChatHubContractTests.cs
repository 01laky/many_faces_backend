using System.Reflection;
using BeDemo.Api.Hubs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiChatHubContractTests
{
    [Fact]
    public void SendToAiWithOperatorStats_has_four_parameters()
    {
        var method = typeof(ChatHub).GetMethod(
            nameof(ChatHub.SendToAiWithOperatorStats),
            BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(4);
        parameters[0].ParameterType.Should().Be(typeof(int));
        parameters[1].ParameterType.Should().Be(typeof(string));
        parameters[2].ParameterType.Should().Be(typeof(string));
        parameters[3].ParameterType.Should().Be(typeof(string));
        parameters[3].Name.Should().Be("responseLocale");
    }

    [Fact]
    public void SendToAi_keeps_two_parameters()
    {
        var method = typeof(ChatHub).GetMethod(
            nameof(ChatHub.SendToAi),
            BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(2);
    }
}
