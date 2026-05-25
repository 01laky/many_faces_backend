using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// OAuth HTTP status + JSON error codes (see docs/guides/authentication-and-sessions.md — OAuth error policy table).
/// Uses default Testing factory (rate limits bypassed) so these tests stay parallel-safe.
/// </summary>
public sealed class OAuthErrorPolicyIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private static readonly JsonSerializerOptions JsonRelaxed = new() { PropertyNameCaseInsensitive = true };

	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public OAuthErrorPolicyIntegrationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = factory.CreateUnscopedClient();
	}

	[Fact]
	public async Task Token_Returns401_InvalidClient_WithOAuthErrorBody()
	{
		var req = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "wrong-secret",
			Username = "any@test.com",
			Password = "Test1234!@##",
		};
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", req);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		var err = await response.Content.ReadFromJsonAsync<OAuth2ErrorResponse>(JsonRelaxed);
		err!.Error.Should().Be("invalid_client");
	}

	[Fact]
	public async Task Token_Returns401_InvalidGrant_WrongPassword_WithOAuthErrorBody()
	{
		var email = $"pol_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var req = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "WrongPassword!!!",
		};
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", req);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		var err = await response.Content.ReadFromJsonAsync<OAuth2ErrorResponse>(JsonRelaxed);
		err!.Error.Should().Be("invalid_grant");
	}

	public void Dispose() => _client.Dispose();
}
