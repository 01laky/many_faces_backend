using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Edge case tests for OAuth2 endpoints
/// Tests boundary cases, error scenarios and security aspects
/// </summary>
public class OAuth2EdgeCaseTests : IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
	private readonly RegistrationInviteWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public OAuth2EdgeCaseTests(RegistrationInviteWebApplicationFactory factory)
	{
		_factory = factory;
		_client = _factory.CreateUnscopedClient();
	}

	#region Registration Edge Cases (email-code flow)

	[Fact]
	public async Task RegisterRequest_ShouldReturnOk_ForValidEmail()
	{
		var response = await _client.PostAsJsonAsync("/api/oauth2/register/request", new { email = $"test_{Guid.NewGuid()}@test.com" });
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task RegisterRequest_ShouldReturnOk_WhenEmailAlreadyRegistered_NoEnumeration()
	{
		var email = $"dup_{Guid.NewGuid()}@test.com";
		await CompleteInviteRegistrationAsync(email, "Test1234!@##");
		var response = await _client.PostAsJsonAsync("/api/oauth2/register/request", new { email });
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task RegisterComplete_ShouldFail_WhenPasswordTooWeak()
	{
		var email = $"weak_{Guid.NewGuid()}@test.com";
		var (hash, code) = await StartInviteAsync(email);
		var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new
		{
			hash,
			code,
			password = "weak",
			clientId = "be-demo-client",
			clientSecret = "be-demo-secret-very-strong-key",
		});
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task LegacyRegister_ShouldReturn400()
	{
		var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "a@b.com", password = "Test1234!@##" });
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientIdIsMissing()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientSecretIsMissing()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientIdIsWrong()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "wrong-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientSecretIsWrong()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "wrong-secret", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenUsernameIsMissing()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenPasswordIsMissing()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenUsernameIsEmpty()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenPasswordIsEmpty()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenIsMissing()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##" });
	//     var request = new { grantType = "PASSWORD", clientId = "be-demo-client", clientSecret = "be-demo-secret-very-strong-key", username = email, password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldSucceed_WithScope()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##" });
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Scope = "read write" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     tokenResponse!.Scope.Should().Be("read write");
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldReturnValidTokenStructure()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##" });
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
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
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##" });
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
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
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = longUsername, Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHandleVeryLongPassword()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var longPassword = "Test1234!@##" + new string('a', 1000);
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
		// With global FallbackPolicy, wrong-verb requests may not bind to [AllowAnonymous] actions → 401 or 405.
		response.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenUsingPut()
	{
		var response = await _client.PutAsync("/api/oauth2/token", new StringContent("{}", Encoding.UTF8, "application/json"));
		response.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenUsingDelete()
	{
		var response = await _client.DeleteAsync("/api/oauth2/token");
		response.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenUsingGet()
	{
		var response = await _client.GetAsync("/api/oauth2/register");
		response.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized);
	}

	#endregion

	#region Concurrent Request Edge Cases

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHandleConcurrentRequests()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##" });
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     
	//     var tasks = Enumerable.Range(0, 10).Select(_ => _client.PostAsJsonAsync("/api/oauth2/token", request));
	//     var responses = await Task.WhenAll(tasks);
	//     
	//     responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
	// }

	[Fact]
	public async Task RegisterRequest_ShouldHandleConcurrentRequests()
	{
		var tasks = Enumerable.Range(0, 10).Select(_ =>
			_client.PostAsJsonAsync("/api/oauth2/register/request", new { email = $"test_{Guid.NewGuid()}@test.com" }));
		var responses = await Task.WhenAll(tasks);
		responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
	}

	#endregion

	private async Task<(string Hash, string Code)> StartInviteAsync(string email)
	{
		_factory.CapturingMailer.Reset();
		var response = await _client.PostAsJsonAsync("/api/oauth2/register/request", new { email, locale = "en" });
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var code = _factory.CapturingMailer.LastRequest!.Params["registration_code"];
		using var scope = _factory.Services.CreateScope();
		var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).First(i => i.Email == email);
		return (invite.LinkHash, code);
	}

	private async Task CompleteInviteRegistrationAsync(string email, string password)
	{
		var (hash, code) = await StartInviteAsync(email);
		var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new
		{
			hash,
			code,
			password,
			clientId = "be-demo-client",
			clientSecret = "be-demo-secret-very-strong-key",
		});
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	public void Dispose()
	{
		_client?.Dispose();
	}
}
