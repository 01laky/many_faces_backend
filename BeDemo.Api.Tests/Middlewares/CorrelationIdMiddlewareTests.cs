using System.Net;
using System.Net.Http;
using BeDemo.Api.Middlewares;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Middlewares;

/// <summary>
/// X13 correlation-id middleware. Unit tests pin the safety allow-list; integration tests pin the end-to-end echo
/// behaviour (generate-when-absent, honour-safe-inbound, discard-unsafe-inbound) through the real HTTP pipeline on an
/// anonymous endpoint (JWKS).
/// </summary>
[Trait("Category", "BackendSecurity")]
public sealed class CorrelationIdMiddlewareUnitTests
{
	[Theory]
	[InlineData("0123456789abcdef0123456789abcdef")] // generated GUID-N shape
	[InlineData("abc-DEF_123.456")]                  // all allowed punctuation
	[InlineData("A")]                                // single char
	public void IsSafeCorrelationId_AcceptsWellFormedValues(string candidate)
	{
		CorrelationIdMiddleware.IsSafeCorrelationId(candidate).Should().BeTrue();
	}

	[Theory]
	[InlineData("")]                       // empty
	[InlineData("has space")]              // whitespace
	[InlineData("line\nbreak")]            // LF — log-injection vector
	[InlineData("carriage\rreturn")]       // CR — header-splitting vector
	[InlineData("tab\tted")]               // control char
	[InlineData("semi;colon")]             // disallowed punctuation
	[InlineData("angle<bracket>")]         // disallowed punctuation
	[InlineData("slash/path")]             // disallowed punctuation
	[InlineData("unicodé")]                // non-ASCII letter
	public void IsSafeCorrelationId_RejectsUnsafeValues(string candidate)
	{
		CorrelationIdMiddleware.IsSafeCorrelationId(candidate).Should().BeFalse();
	}

	[Fact]
	public void IsSafeCorrelationId_RejectsOverLength()
	{
		var tooLong = new string('a', CorrelationIdMiddleware.MaxLength + 1);
		CorrelationIdMiddleware.IsSafeCorrelationId(tooLong).Should().BeFalse();

		var atLimit = new string('a', CorrelationIdMiddleware.MaxLength);
		CorrelationIdMiddleware.IsSafeCorrelationId(atLimit).Should().BeTrue();
	}
}

/// <summary>End-to-end echo behaviour through the real pipeline.</summary>
[Trait("Category", "BackendSecurity")]
public sealed class CorrelationIdMiddlewareIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private const string Jwks = "/api/oauth2/jwks";
	private readonly CustomWebApplicationFactory<Program> _factory;

	public CorrelationIdMiddlewareIntegrationTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	private static string CorrelationHeaderValue(HttpResponseMessage response)
	{
		response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationHeader, out var values)
			.Should().BeTrue("every response must carry {0}", CorrelationIdMiddleware.CorrelationHeader);
		return string.Join(",", values!);
	}

	[Fact]
	public async Task GeneratesCorrelationId_WhenNoneSupplied()
	{
		using var client = _factory.CreateUnscopedClient();

		using var response = await client.GetAsync(Jwks);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var id = CorrelationHeaderValue(response);
		CorrelationIdMiddleware.IsSafeCorrelationId(id).Should().BeTrue();
		id.Should().HaveLength(32, "a generated id is a GUID in 'N' format");
	}

	[Fact]
	public async Task EchoesSafeInboundCorrelationId()
	{
		using var client = _factory.CreateUnscopedClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, Jwks);
		request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.CorrelationHeader, "trace-abc_123.XYZ");

		using var response = await client.SendAsync(request);

		CorrelationHeaderValue(response).Should().Be("trace-abc_123.XYZ");
	}

	[Fact]
	public async Task HonoursSafeInbound_RequestIdHeader()
	{
		using var client = _factory.CreateUnscopedClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, Jwks);
		request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.RequestIdHeader, "gateway-id-42");

		using var response = await client.SendAsync(request);

		CorrelationHeaderValue(response).Should().Be("gateway-id-42");
	}

	[Fact]
	public async Task DiscardsUnsafeInbound_AndGeneratesServerId()
	{
		using var client = _factory.CreateUnscopedClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, Jwks);
		// Space is not in the allow-list — TryAddWithoutValidation lets us send it past HttpClient's own validation.
		request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.CorrelationHeader, "evil value with spaces");

		using var response = await client.SendAsync(request);

		var id = CorrelationHeaderValue(response);
		id.Should().NotBe("evil value with spaces");
		CorrelationIdMiddleware.IsSafeCorrelationId(id).Should().BeTrue("the server must substitute a safe generated id");
	}
}
