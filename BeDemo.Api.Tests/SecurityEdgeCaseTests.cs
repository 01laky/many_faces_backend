using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Security-related edge cases for OAuth2 token/register inputs (injection-ish payloads, O4 signature rejection).
/// </summary>
public class SecurityEdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public SecurityEdgeCaseTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Token_ShouldFail_WhenSQLInjectionInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "admin' OR '1'='1", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenXSSInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "<script>alert('xss')</script>", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenSQLInjectionInEmail()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			"test' OR '1'='1@test.com",
			"Test1234!@##");
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenXSSInEmail()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			"<script>alert('xss')</script>@test.com",
			"Test1234!@##");
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenPathTraversalInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "../../etc/passwd", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenCommandInjectionInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test; rm -rf /", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenNullByteInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test\0@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenUnicodeNullInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test\u0000@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenNewlineInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test\n@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenCarriageReturnInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test\r@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenTabInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test\t@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenOnlyWhitespaceInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "   ", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenOnlyWhitespaceInPassword()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "   " };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientIdContainsSQLInjection()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "admin' OR '1'='1", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenClientSecretContainsSQLInjection()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "admin' OR '1'='1", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenGrantTypeContainsSQLInjection()
	{
		var request = new OAuth2TokenRequest { GrantType = "password' OR '1'='1", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenVeryLongClientId()
	{
		var longClientId = new string('a', 10000);
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = longClientId, ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenVeryLongClientSecret()
	{
		var longSecret = new string('a', 10000);
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = longSecret, Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenVeryLongGrantType()
	{
		var longGrantType = new string('a', 10000);
		var request = new OAuth2TokenRequest { GrantType = longGrantType, ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenEmailContainsNullByte()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			"test\0@test.com",
			"Test1234!@##");
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Register_ShouldFail_WhenPasswordContainsNullByte()
	// {
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = "test@test.com", password = "Test123!\0@#" });
	//     response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	// }

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenContainsSQLInjection()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "token' OR '1'='1" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenRefreshTokenContainsXSS()
	{
		var request = new OAuth2TokenRequest { GrantType = "refresh_token", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", RefreshToken = "<script>alert('xss')</script>" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldFail_WhenScopeContainsSQLInjection()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Scope = "read' OR '1'='1" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     // Scope injection shouldn't break the flow, but should be sanitized
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	[Fact]
	public async Task Token_ShouldFail_WhenSignatureAlgorithmIsInvalid()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Signature = "test", SignatureAlgorithm = "INVALID" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		// O4: body signatures are rejected with 400 invalid_request (not verified against server key).
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenSignatureIsInvalidBase64()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Signature = "not-valid-base64!!!", SignatureAlgorithm = "ES512" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldFail_WhenSignatureIsEmpty()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Signature = "", SignatureAlgorithm = "ES512" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldSucceed_WhenSignatureIsNotProvided()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	public void Dispose()
	{
		_client?.Dispose();
	}
}
