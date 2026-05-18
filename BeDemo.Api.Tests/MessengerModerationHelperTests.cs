using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public class MessengerModerationHelperTests
{
    [Fact]
    public void ShouldHidePeerConversation_ShouldHidePeersButNotSuperAdmin()
    {
        var superIds = new HashSet<string>(StringComparer.Ordinal) { "super-1" };
        MessengerModerationHelper.ShouldHidePeerConversation(true, "peer-1", superIds).Should().BeTrue();
        MessengerModerationHelper.ShouldHidePeerConversation(true, "super-1", superIds).Should().BeFalse();
        MessengerModerationHelper.ShouldHidePeerConversation(false, "peer-1", superIds).Should().BeFalse();
    }
}
