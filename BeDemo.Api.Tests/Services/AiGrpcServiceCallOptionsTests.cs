using System.Reflection;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Services;

/// <summary>
/// Pins the AiGrpcService timeout/cancellation fix. <c>CreateCallOptions</c> used to build a linked
/// <see cref="CancellationTokenSource"/> with <c>using</c> and dispose it before the call ran, so its
/// <c>CancelAfter</c> timer never fired and its token came from a disposed source. The fix passes the
/// caller's token straight through and relies on the gRPC deadline for the timeout. These tests reach the
/// private builder by reflection (no gRPC channel is created — the channel is lazy).
/// </summary>
public sealed class AiGrpcServiceCallOptionsTests
{
	private static (AiGrpcService Service, MethodInfo Create) Build()
	{
		var service = new AiGrpcService(
			Options.Create(new AiServiceOptions { GrpcAddress = "http://localhost:50051" }),
			new ConfigurationBuilder().Build(),
			NullLogger<AiGrpcService>.Instance);
		var create = typeof(AiGrpcService)
			.GetMethod("CreateCallOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
		create.Should().NotBeNull("CreateCallOptions exists on AiGrpcService");
		return (service, create);
	}

	[Fact]
	public void CreateCallOptions_passes_the_caller_token_and_sets_a_future_deadline()
	{
		var (service, create) = Build();
		using var disposable = service;
		using var cts = new CancellationTokenSource();

		var options = (CallOptions)create.Invoke(service, new object?[] { cts.Token, null })!;

		options.CancellationToken.Should().Be(cts.Token, "the caller's token must flow straight through");
		options.CancellationToken.IsCancellationRequested.Should().BeFalse();
		options.Deadline.Should().NotBeNull("a gRPC deadline bounds the call");
		options.Deadline!.Value.Should().BeAfter(DateTime.UtcNow);
	}

	[Fact]
	public void CreateCallOptions_token_observes_caller_cancellation()
	{
		var (service, create) = Build();
		using var disposable = service;
		using var cts = new CancellationTokenSource();

		var options = (CallOptions)create.Invoke(service, new object?[] { cts.Token, null })!;
		cts.Cancel();

		// Regression: the returned token IS the caller's token, so cancelling the caller cancels the call
		// (previously the token came from a disposed linked source and could not be observed/threw).
		options.CancellationToken.IsCancellationRequested.Should().BeTrue();
	}

	[Fact]
	public void CreateCallOptions_honours_a_per_call_deadline_override()
	{
		var (service, create) = Build();
		using var disposable = service;
		using var cts = new CancellationTokenSource();

		var before = DateTime.UtcNow;
		var options = (CallOptions)create.Invoke(service, new object?[] { cts.Token, TimeSpan.FromSeconds(5) })!;

		options.Deadline.Should().NotBeNull();
		options.Deadline!.Value.Should().BeOnOrAfter(before.AddSeconds(5).AddSeconds(-2));
		options.Deadline!.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5).AddSeconds(2));
	}
}
