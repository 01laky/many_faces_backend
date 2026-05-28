using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP12 edge cases (BE-RP12-U1…U2).</summary>
public sealed class BeRp12CacheHeadersEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp12CacheHeadersEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP12-U1 — localization bundle returns Cache-Control public max-age.</summary>
	[Fact]
	public async Task BE_RP12_U1_Localization_HasCacheControl_ContentUnchanged()
	{
		using var client = _factory.CreateUnscopedClient();
		var first = await client.GetAsync("/api/localization/portal");
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		first.Headers.CacheControl.Should().NotBeNull();
		first.Headers.CacheControl!.Public.Should().BeTrue();
		first.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.Zero);

		var body1 = await first.Content.ReadAsStringAsync();
		var second = await client.GetAsync("/api/localization/portal");
		(await second.Content.ReadAsStringAsync()).Should().Be(body1);
	}

	/// <summary>BE-RP12-U2 — authenticated faces config uses private cache, anonymous uses public.</summary>
	[Fact]
	public async Task BE_RP12_U2_FacesConfig_AnonymousPublic_AuthenticatedPrivate()
	{
		using var anonClient = _factory.CreateFaceClient("public");
		var anonRes = await anonClient.GetAsync("/api/faces/config");
		anonRes.Headers.CacheControl!.Public.Should().BeTrue();
		anonRes.Headers.CacheControl.Private.Should().BeFalse();

		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth);
		using var authClient = _factory.CreateFaceClient("public");
		authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var authRes = await authClient.GetAsync("/api/faces/config");
		authRes.Headers.CacheControl!.Private.Should().BeTrue();
		authRes.Headers.CacheControl.Public.Should().BeFalse();
	}

	/// <summary>BE-RP12-U1b — public stats endpoint exposes short public cache.</summary>
	[Fact]
	public async Task BE_RP12_U1b_PublicStats_HasPublicCacheControl()
	{
		using var client = _factory.CreateFaceClient("public");
		var response = await client.GetAsync("/api/Stats/public");
		response.EnsureSuccessStatusCode();
		response.Headers.CacheControl!.Public.Should().BeTrue();
	}
}
