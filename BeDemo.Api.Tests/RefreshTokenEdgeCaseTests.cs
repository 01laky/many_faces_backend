using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Refresh token grant edge cases (empty refresh, reuse after rotation, access JWT misused as refresh).
/// </summary>
public class RefreshTokenEdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public RefreshTokenEdgeCaseTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Token_ShouldSucceed_WithValidRefreshToken()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "Test1234!@##",
		};
		var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
		tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		tokenData!.RefreshToken.Should().NotBeNullOrEmpty();

		var refreshRequest = new OAuth2TokenRequest
		{
			GrantType = "refresh_token",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			RefreshToken = tokenData.RefreshToken,
		};
		var refreshResponse = await _client.PostAsJsonAsync("/api/oauth2/token", refreshRequest);
		refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Token_ShouldReturnNewAccessAndRefresh_WhenRefreshing()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "Test1234!@##",
		};
		var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
		var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		var refreshRequest = new OAuth2TokenRequest
		{
			GrantType = "refresh_token",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			RefreshToken = tokenData!.RefreshToken,
		};
		var refreshResponse = await _client.PostAsJsonAsync("/api/oauth2/token", refreshRequest);
		var refreshData = await refreshResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		refreshData!.AccessToken.Should().NotBe(tokenData.AccessToken);
		refreshData.RefreshToken.Should().NotBe(tokenData.RefreshToken);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsEmpty()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsNull()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = null };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsRandomString()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "random-invalid-token-string" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsAccessToken()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var tokenRequest = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
		var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
		var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		var refreshRequest = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = tokenData!.AccessToken };
		var refreshResponse = await _client.PostAsJsonAsync("/api/oauth2/token", refreshRequest);
		refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsUsedTwice()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var tokenRequest = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
		var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
		var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

		var refreshRequest = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = tokenData!.RefreshToken };
		var first = await _client.PostAsJsonAsync("/api/oauth2/token", refreshRequest);
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		var secondRefresh = await _client.PostAsJsonAsync("/api/oauth2/token", refreshRequest);
		secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	public void Dispose()
	{
		_client?.Dispose();
	}
}
