using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Guards <see cref="ChatHistoryEntry"/> in Models/DTOs used by <see cref="BeDemo.Api.Hubs.ChatHub"/> SignalR contracts.</summary>
public sealed class ChatHistoryDtosTests
{
	[Fact]
	public void ChatHistoryEntry_exposes_user_and_ai_properties()
	{
		var entry = new ChatHistoryEntry
		{
			UserMessage = "hello",
			AiResponse = "hi",
		};

		entry.UserMessage.Should().Be("hello");
		entry.AiResponse.Should().Be("hi");
	}
}
