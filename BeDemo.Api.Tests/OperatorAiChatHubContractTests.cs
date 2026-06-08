using System.Reflection;
using BeDemo.Api.Hubs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiChatHubContractTests
{
	// After the RAG retrieval refactor (D10/D11) the operator chat is always data-grounded:
	// the `statsMode` and `responseLocale` parameters are removed. Only the conversation id,
	// the message, and the optional per-call parallel cap remain.
	[Fact]
	public void SendToAiWithOperatorStats_has_three_parameters_without_mode_or_locale()
	{
		var method = typeof(ChatHub).GetMethod(
			nameof(ChatHub.SendToAiWithOperatorStats),
			BindingFlags.Instance | BindingFlags.Public);
		method.Should().NotBeNull();
		var parameters = method!.GetParameters();
		parameters.Should().HaveCount(3);
		parameters[0].ParameterType.Should().Be(typeof(int));
		parameters[1].ParameterType.Should().Be(typeof(string));
		parameters[2].ParameterType.Should().Be(typeof(int?));
		parameters[2].Name.Should().Be("maxParallelBundleAiCalls");
		parameters[2].HasDefaultValue.Should().BeTrue();

		// The removed dimensions (D10/D11) must not reappear as parameters.
		parameters.Should().NotContain(p => p.Name == "responseLocale");
		parameters.Should().NotContain(p => p.Name == "statsMode");
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
