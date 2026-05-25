using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

public class OAuth2ControllerTests : IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
	private readonly RegistrationInviteWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public OAuth2ControllerTests(RegistrationInviteWebApplicationFactory factory)
	{
		_factory = factory;
		_client = _factory.CreateUnscopedClient();
	}

	[Fact]
	public async Task Register_ShouldReturnDeprecated_WhenLegacyEndpointUsed()
	{
		var response = await _client.PostAsJsonAsync("/api/oauth2/register", new
		{
			email = $"test_{Guid.NewGuid()}@test.com",
			password = "Test1234!@##",
		});
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldReturnToken_WhenValidCredentials()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		await RegisterViaInviteFlowAsync(email, password);

		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = password
		};

		// Act - retry logic with exponential backoff for in-memory database timing issues
		HttpResponseMessage? response = null;
		for (int i = 0; i < 15; i++)
		{
			await Task.Delay(150 * (i + 1)); // Exponential backoff: 150ms, 300ms, 450ms...
			response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
			if (response.StatusCode == HttpStatusCode.OK)
				break;
		}

		// Assert
		response.Should().NotBeNull();
		response!.StatusCode.Should().Be(HttpStatusCode.OK);
		var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		tokenResponse.Should().NotBeNull();
		tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
		tokenResponse.TokenType.Should().Be("Bearer");
		tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task Token_ShouldReturnUnauthorized_WhenInvalidCredentials()
	{
		// Arrange
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "invalid@test.com",
			Password = "WrongPassword123!"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		var errorResponse = await response.Content.ReadFromJsonAsync<OAuth2ErrorResponse>();
		errorResponse.Should().NotBeNull();
		errorResponse!.Error.Should().Be("invalid_grant");
	}

	[Fact]
	public async Task Token_ShouldReturnUnauthorized_WhenInvalidClient()
	{
		// Arrange
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "invalid-client",
			ClientSecret = "invalid-secret",
			Username = "test@test.com",
			Password = "Test123!"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	private Task RegisterViaInviteFlowAsync(string email, string password) =>
		IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password);

	public void Dispose()
	{
		_client?.Dispose();
	}
}
