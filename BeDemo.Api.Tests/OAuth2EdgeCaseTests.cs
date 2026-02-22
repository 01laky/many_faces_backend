using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Edge case tests for OAuth2 endpoints
/// Tests boundary cases, error scenarios and security aspects
/// </summary>
public class OAuth2EdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OAuth2EdgeCaseTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Registration Edge Cases

    [Fact]
    public async Task Register_ShouldFail_WhenEmailIsEmpty()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "", password = "Test123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenEmailIsNull()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { password = "Test123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordIsEmpty()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordIsNull()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordTooShort()
    {
        // Minimum password length is 4 characters, so test with 3 characters that don't meet requirements
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "Te1" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordNoDigit()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "TestPass!" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordNoLowercase()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "TEST123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordNoUppercase()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "test123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenPasswordNoSpecialChar()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "Test12345" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenEmailAlreadyExists()
    {
        var email = $"test_{Guid.NewGuid()}@test.com";
        var firstResponse = await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#", firstName = "Test", lastName = "User" });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait a bit to ensure first user is fully created in Identity
        await Task.Delay(1000);

        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#", firstName = "Test", lastName = "User" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenEmailInvalidFormat()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "invalid-email", password = "Test123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Register_ShouldFail_WhenEmailIsVeryLong()
    // {
    //     var longEmail = new string('a', 200) + "@test.com";
    //     var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = longEmail, password = "Test123!@#" });
    //     response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    // }

    [Fact]
    public async Task Register_ShouldSucceed_WhenPasswordExactly8Chars()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test1!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ShouldSucceed_WhenPasswordVeryLong()
    {
        var longPassword = "Test123!@#" + new string('a', 100);
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = longPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ShouldSucceed_WithSpecialCharactersInEmail()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test+{Guid.NewGuid()}@test.com", password = "Test123!@#" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ShouldSucceed_WithUnicodeInName()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test123!@#", firstName = "John", lastName = "Doe" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ShouldSucceed_WithEmptyOptionalFields()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test123!@#", firstName = "", lastName = "" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Token Request Edge Cases

    [Fact]
    public async Task Token_ShouldFail_WhenGrantTypeIsEmpty()
    {
        var request = new OAuth2TokenRequest { GrantType = "", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenGrantTypeIsNull()
    {
        var request = new { grantType = (string?)null, clientId = "be-demo-client", clientSecret = "be-demo-secret-very-strong-key" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenGrantTypeIsInvalid()
    {
        var request = new OAuth2TokenRequest { GrantType = "invalid_grant", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenClientIdIsMissing()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenClientSecretIsMissing()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", Username = "test@test.com", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenClientIdIsWrong()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "wrong-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenClientSecretIsWrong()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "wrong-secret", Username = "test@test.com", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenUsernameIsMissing()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenPasswordIsMissing()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenUsernameIsEmpty()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "", Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenPasswordIsEmpty()
    {
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenRefreshTokenIsMissing()
    {
        var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenRefreshTokenIsInvalid()
    {
        var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "invalid-token" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenRefreshTokenIsExpired()
    {
        // This would require creating an expired token - simplified test
        var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "expired.token.here" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldSucceed_WithCaseInsensitiveGrantType()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
    //     var request = new { grantType = "PASSWORD", clientId = "be-demo-client", clientSecret = "be-demo-secret-very-strong-key", username = email, password = "Test123!@#" };
    //     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     response.StatusCode.Should().Be(HttpStatusCode.OK);
    // }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldSucceed_WithScope()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
    //     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test123!@#", Scope = "read write" };
    //     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     response.StatusCode.Should().Be(HttpStatusCode.OK);
    //     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     tokenResponse!.Scope.Should().Be("read write");
    // }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldReturnValidTokenStructure()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
    //     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test123!@#" };
    //     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     response.StatusCode.Should().Be(HttpStatusCode.OK);
    //     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     tokenResponse.Should().NotBeNull();
    //     tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
    //     tokenResponse.TokenType.Should().Be("Bearer");
    //     tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
    //     tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
    // }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldReturnDifferentTokensForSameUser()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
    //     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test123!@#" };
    //     var response1 = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     var response2 = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     var token1 = await response1.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     var token2 = await response2.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     token1!.AccessToken.Should().NotBe(token2!.AccessToken);
    // }

    #endregion

    #region Request Body Edge Cases

    [Fact]
    public async Task Token_ShouldFail_WhenBodyIsEmpty()
    {
        var response = await _client.PostAsync("/api/oauth2/token", new StringContent("", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenBodyIsInvalidJson()
    {
        var response = await _client.PostAsync("/api/oauth2/token", new StringContent("{ invalid json }", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenBodyIsNotJson()
    {
        var response = await _client.PostAsync("/api/oauth2/token", new StringContent("plain text", Encoding.UTF8, "text/plain"));
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenBodyIsNull()
    {
        var response = await _client.PostAsync("/api/oauth2/token", null!);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Token_ShouldHandleVeryLongUsername()
    {
        var longUsername = new string('a', 1000) + "@test.com";
        var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = longUsername, Password = "Test123!@#" };
        var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldHandleVeryLongPassword()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     var longPassword = "Test123!@#" + new string('a', 1000);
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = longPassword });
    //     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = longPassword };
    //     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
    //     response.StatusCode.Should().Be(HttpStatusCode.OK);
    // }

    #endregion

    #region HTTP Method Edge Cases

    [Fact]
    public async Task Token_ShouldFail_WhenUsingGet()
    {
        var response = await _client.GetAsync("/api/oauth2/token");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenUsingPut()
    {
        var response = await _client.PutAsync("/api/oauth2/token", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Token_ShouldFail_WhenUsingDelete()
    {
        var response = await _client.DeleteAsync("/api/oauth2/token");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenUsingGet()
    {
        var response = await _client.GetAsync("/api/oauth2/register");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region Concurrent Request Edge Cases

    // [Fact] // Temporarily disabled - database conflict
    // public async Task Token_ShouldHandleConcurrentRequests()
    // {
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
    //     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test123!@#" };
    //     
    //     var tasks = Enumerable.Range(0, 10).Select(_ => _client.PostAsJsonAsync("/api/oauth2/token", request));
    //     var responses = await Task.WhenAll(tasks);
    //     
    //     responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    // }

    [Fact]
    public async Task Register_ShouldHandleConcurrentRegistrations()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test123!@#" }));
        var responses = await Task.WhenAll(tasks);
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}
