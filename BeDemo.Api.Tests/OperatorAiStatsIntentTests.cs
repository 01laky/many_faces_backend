using BeDemo.Api.Utils;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiStatsIntentTests
{
    [Theory]
    [InlineData("Koľko máme používateľov?", true)]
    [InlineData("How many users in dashboard?", true)]
    [InlineData("faceWallTicketsByStatus", true)]
    [InlineData("Koľko je hodín?", false)]
    [InlineData("What time is it now?", false)]
    [InlineData("Explain SignalR in our project", false)]
    public void IsMetricsQuestion_classifies_messages(string message, bool expected)
    {
        Assert.Equal(expected, OperatorAiStatsIntent.IsMetricsQuestion(message));
    }
}
