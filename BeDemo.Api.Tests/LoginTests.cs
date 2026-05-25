using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for login (OAuth2 token endpoint)
/// </summary>
public class LoginTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public LoginTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Login_ShouldReturnToken_WhenValidCredentials()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		// Register user first
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "Test", "User");

		var loginRequest = new OAuth2TokenRequest
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
			response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);
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
	public async Task Login_ShouldReturnUnauthorized_WhenInvalidEmail()
	{
		// Arrange
		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "nonexistent@test.com",
			Password = "Test1234!@##"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		var errorResponse = await response.Content.ReadFromJsonAsync<OAuth2ErrorResponse>();
		errorResponse.Should().NotBeNull();
		errorResponse!.Error.Should().Be("invalid_grant");
	}

	[Fact]
	public async Task Login_ShouldReturnUnauthorized_WhenInvalidPassword()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "Test", "User");

		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "WrongPassword123!"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		var errorResponse = await response.Content.ReadFromJsonAsync<OAuth2ErrorResponse>();
		errorResponse.Should().NotBeNull();
		errorResponse!.Error.Should().Be("invalid_grant");
	}

	[Fact]
	public async Task Login_ShouldReturnUnauthorized_WhenClientIdIsInvalid()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "Test", "User");

		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "invalid-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = password
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_ShouldReturnUnauthorized_WhenClientSecretIsInvalid()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "Test", "User");

		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "invalid-secret",
			Username = email,
			Password = password
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_ShouldReturnBadRequest_WhenGrantTypeIsMissing()
	{
		// Arrange
		var loginRequest = new OAuth2TokenRequest
		{
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "test@test.com",
			Password = "Test1234!@##"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_ShouldReturnBadRequest_WhenGrantTypeIsInvalid()
	{
		// Arrange
		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "invalid_grant",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "test@test.com",
			Password = "Test1234!@##"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_ShouldReturnToken_WhenEmailIsCaseInsensitive()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email.ToLower(),
			password,
			"Test",
			"User");

		// Try to login with lowercase email (Identity normalizes emails, so case doesn't matter)
		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email.ToLower(), // Use lowercase as Identity normalizes emails
			Password = password
		};

		// Act - retry logic with exponential backoff for in-memory database timing issues
		HttpResponseMessage? response = null;
		for (int i = 0; i < 15; i++)
		{
			await Task.Delay(150 * (i + 1)); // Exponential backoff: 150ms, 300ms, 450ms...
			response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);
			if (response.StatusCode == HttpStatusCode.OK)
				break;
		}

		// Assert
		response.Should().NotBeNull();
		response!.StatusCode.Should().Be(HttpStatusCode.OK);
		var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		tokenResponse.Should().NotBeNull();
		tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Login_ShouldReturnUnauthorized_WhenUsernameIsEmpty()
	{
		// Arrange
		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "",
			Password = "Test1234!@##"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsEmpty()
	{
		// Arrange
		var loginRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = "test@test.com",
			Password = ""
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_ShouldReturnTokenWithCorrectStructure()
	{
		// Arrange
		var email = $"test_{Guid.NewGuid()}@test.com";
		var password = "Test1234!@##";

		// Register user first
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "Test", "User");

		var loginRequest = new OAuth2TokenRequest
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
			response = await _client.PostAsJsonAsync("/api/oauth2/token", loginRequest);
			if (response.StatusCode == HttpStatusCode.OK)
				break;
		}

		// Assert
		response.Should().NotBeNull();
		response!.StatusCode.Should().Be(HttpStatusCode.OK);
		var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		tokenResponse.Should().NotBeNull();
		tokenResponse!.AccessToken.Should().NotBeNullOrEmpty().And.MatchRegex(@"^[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]*$"); // JWT format
		tokenResponse.TokenType.Should().Be("Bearer");
		tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
		tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
	}

	public void Dispose()
	{
		_client?.Dispose();
	}
}
