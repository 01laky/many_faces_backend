using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using BeDemo.Api.Middlewares;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace BeDemo.Api.Tests.Middlewares;

/// <summary>
/// Phase-4 observability: <c>UseSerilogRequestLogging</c> emits one structured completion event per request, enriched
/// with the X13 correlation id. The request-logging middleware resolves the static <see cref="Log.Logger"/> lazily,
/// so the test swaps in a capturing sink, drives a request with a known inbound correlation id through the real
/// pipeline, and asserts the completion event carries that id as the <c>CorrelationId</c> property.
/// </summary>
[Trait("Category", "BackendInfra")]
public sealed class SerilogRequestLoggingTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public SerilogRequestLoggingTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	private sealed class CapturingSink : ILogEventSink
	{
		public ConcurrentQueue<LogEvent> Events { get; } = new();
		public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
	}

	[Fact]
	public async Task RequestCompletionEvent_CarriesCorrelationId()
	{
		// Boot the app first (the factory configures Log.Logger during startup), THEN swap in the capturing sink.
		using var client = _factory.CreateUnscopedClient();
		var correlationId = "serilog-rl-" + Guid.NewGuid().ToString("N");

		var sink = new CapturingSink();
		var original = Log.Logger;
		Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, "/api/oauth2/jwks");
			request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.CorrelationHeader, correlationId);
			using var response = await client.SendAsync(request);
			response.StatusCode.Should().Be(HttpStatusCode.OK);

			// Find the request-COMPLETION event for OUR request: it carries our correlation id AND the
			// RequestPath/StatusCode properties (scoped per-request app log lines carry the id too — skip those).
			var completion = sink.Events.FirstOrDefault(e =>
				e.Properties.TryGetValue("CorrelationId", out var prop) &&
				prop is ScalarValue { Value: string s } &&
				s == correlationId &&
				e.Properties.ContainsKey("RequestPath") &&
				e.Properties.ContainsKey("StatusCode"));

			completion.Should().NotBeNull("the Serilog request-logging middleware must emit a completion event enriched with the X13 correlation id");
			((completion!.Properties["RequestPath"] as ScalarValue)!.Value as string).Should().Be("/api/oauth2/jwks");
		}
		finally
		{
			Log.Logger = original;
		}
	}
}
