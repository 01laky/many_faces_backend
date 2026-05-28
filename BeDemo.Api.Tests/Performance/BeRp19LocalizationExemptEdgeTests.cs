using System.Net;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP19 edge case (BE-RP19-U1).</summary>
public sealed class BeRp19LocalizationExemptEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp19LocalizationExemptEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP19-U1 — GET /api/localization/portal without face prefix returns 200 (exempt path).</summary>
	[Fact]
	public async Task BE_RP19_U1_LocalizationPortal_UnscopedPath_Returns200()
	{
		using var client = _factory.CreateUnscopedClient();
		var response = await client.GetAsync("/api/localization/portal");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("\"app\":\"portal\"");
	}
}
