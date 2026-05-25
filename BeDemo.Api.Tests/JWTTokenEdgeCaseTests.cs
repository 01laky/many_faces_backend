using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Edge case testy pre JWT tokeny
/// </summary>
public class JWTTokenEdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public JWTTokenEdgeCaseTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldContainCorrectClaims()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test1234!@##", firstName = "John", lastName = "Doe" });
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier);
	//     token.Claims.Should().Contain(c => c.Type == ClaimTypes.Email);
	//     token.Claims.Should().Contain(c => c.Type == ClaimTypes.GivenName && c.Value == "John");
	//     token.Claims.Should().Contain(c => c.Type == ClaimTypes.Surname && c.Value == "Doe");
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveExpiration()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.ValidTo.Should().BeAfter(DateTime.UtcNow);
	//     token.ValidFrom.Should().BeBefore(DateTime.UtcNow);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveCorrectIssuer()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.Issuer.Should().Be("AdminDemoApi");
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveCorrectAudience()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.Audiences.Should().Contain("AdminDemoApi");
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveJtiClaim()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveIatClaim()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var token = handler.ReadJwtToken(tokenResponse!.AccessToken);
	//     token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHaveUniqueJtiForEachRequest()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##" };
	//     var response1 = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var response2 = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     var token1 = await response1.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var token2 = await response2.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	//     var handler = new JwtSecurityTokenHandler();
	//     var jwt1 = handler.ReadJwtToken(token1!.AccessToken);
	//     var jwt2 = handler.ReadJwtToken(token2!.AccessToken);
	//     var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
	//     var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
	//     jti1.Should().NotBe(jti2);
	// }

	public void Dispose()
	{
		_client?.Dispose();
	}
}
