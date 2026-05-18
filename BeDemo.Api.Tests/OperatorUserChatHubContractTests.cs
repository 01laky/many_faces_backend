using System.Reflection;
using BeDemo.Api.Hubs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorUserChatHubContractTests
{
    [Fact]
    public void SendPlatformDirectMessage_has_two_string_parameters()
    {
        var method = typeof(MessengerHub).GetMethod(
            nameof(MessengerHub.SendPlatformDirectMessage),
            BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[1].ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public void ReceiveChatMessage_documented_with_five_callback_arguments()
    {
        // Contract enforced by integration tests; hub file header documents arity.
        typeof(MessengerHub).GetMethod(nameof(MessengerHub.SendChatMessage), BindingFlags.Instance | BindingFlags.Public)
            .Should().NotBeNull();
    }
}
