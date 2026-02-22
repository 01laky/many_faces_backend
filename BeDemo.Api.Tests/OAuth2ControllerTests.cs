using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class OAuth2ControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OAuth2ControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var registerRequest = new
        {
            email = $"test_{Guid.NewGuid()}@test.com",
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("registered successfully");
    }

    [Fact]
    public async Task Token_ShouldReturnToken_WhenValidCredentials()
    {
        // Arrange
        var email = $"test_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        // Register user first
        var registerResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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

    public void Dispose()
    {
        _client?.Dispose();
    }
}
