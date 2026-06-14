using System.Reflection;
using BeDemo.Api.Hubs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiChatHubContractTests
{
	// After the RAG retrieval refactor (D10/D11) the operator chat is always data-grounded:
	// the `statsMode` and `responseLocale` parameters are removed. The wire contract is exactly
	// (conversationId, message) — see the regression note below on why there is NO third parameter.
	[Fact]
	public void SendToAiWithOperatorStats_has_exactly_conversationId_and_message()
	{
		var method = typeof(ChatHub).GetMethod(
			nameof(ChatHub.SendToAiWithOperatorStats),
			BindingFlags.Instance | BindingFlags.Public);
		method.Should().NotBeNull();
		var parameters = method!.GetParameters();

		// REGRESSION GUARD: this MUST stay at two non-optional parameters. ASP.NET Core SignalR does not
		// support optional / defaulted hub-method parameters — the admin SPA invokes with two arguments
		// (conversationId, message), and a third (even optional) parameter makes argument binding fail on
		// the server before the method body runs, silently breaking every operator AI chat turn. The old
		// `maxParallelBundleAiCalls` override was therefore removed; the cap comes from OperatorAiOptions.
		parameters.Should().HaveCount(2);
		parameters[0].ParameterType.Should().Be(typeof(int));
		parameters[0].Name.Should().Be("conversationId");
		parameters[1].ParameterType.Should().Be(typeof(string));
		parameters[1].Name.Should().Be("message");
		parameters.Should().NotContain(p => p.HasDefaultValue);

		// The removed dimensions (D10/D11) must not reappear as parameters.
		parameters.Should().NotContain(p => p.Name == "responseLocale");
		parameters.Should().NotContain(p => p.Name == "statsMode");
		parameters.Should().NotContain(p => p.Name == "maxParallelBundleAiCalls");
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
