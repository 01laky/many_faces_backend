using System.Text.Json;
using BeDemo.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeDemo.Api.Tests.Middlewares;

/// <summary>
/// X4 global exception handler. Pins the response shape (RFC 7807 problem+json 500 with a traceId) and the leak
/// discipline: the exception detail appears only in Development, never otherwise.
/// </summary>
[Trait("Category", "BackendSecurity")]
public sealed class ProblemDetailsExceptionHandlerTests
{
	private sealed class FakeHostEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Production;
		public string ApplicationName { get; set; } = "tests";
		public string ContentRootPath { get; set; } = string.Empty;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}

	private static async Task<(int status, string contentType, JsonElement body, bool handled)> HandleAsync(
		string environmentName, Exception exception)
	{
		var context = new DefaultHttpContext();
		context.TraceIdentifier = "corr-test-123";
		context.Request.Path = "/some/path";
		context.Response.Body = new MemoryStream();

		var handler = new ProblemDetailsExceptionHandler(
			NullLogger<ProblemDetailsExceptionHandler>.Instance,
			new FakeHostEnvironment { EnvironmentName = environmentName });

		var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

		context.Response.Body.Position = 0;
		using var reader = new StreamReader(context.Response.Body);
		var text = await reader.ReadToEndAsync();
		using var doc = JsonDocument.Parse(text);
		return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, doc.RootElement.Clone(), handled);
	}

	[Fact]
	public async Task Writes_ProblemJson_500_WithTraceId()
	{
		var (status, contentType, body, handled) = await HandleAsync(
			Environments.Production, new InvalidOperationException("boom secret detail"));

		handled.Should().BeTrue();
		status.Should().Be(StatusCodes.Status500InternalServerError);
		contentType.Should().StartWith("application/problem+json");
		body.GetProperty("status").GetInt32().Should().Be(500);
		body.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
		body.GetProperty("traceId").GetString().Should().Be("corr-test-123");
	}

	[Fact]
	public async Task Production_DoesNotLeak_ExceptionDetail()
	{
		var (_, _, body, _) = await HandleAsync(
			Environments.Production, new InvalidOperationException("boom secret detail"));

		// No "detail" member at all (and certainly not the exception message) outside Development.
		body.TryGetProperty("detail", out var detail).Should().BeFalse();
		body.GetRawText().Should().NotContain("boom secret detail");
	}

	[Fact]
	public async Task Development_Includes_ExceptionDetail()
	{
		var (_, _, body, _) = await HandleAsync(
			Environments.Development, new InvalidOperationException("boom secret detail"));

		body.TryGetProperty("detail", out var detail).Should().BeTrue();
		detail.GetString().Should().Contain("boom secret detail");
	}
}
