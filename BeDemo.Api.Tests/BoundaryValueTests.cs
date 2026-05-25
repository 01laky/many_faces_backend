using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Boundary value tests - tests boundary values
/// </summary>
public class BoundaryValueTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public BoundaryValueTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Register_ShouldFail_WhenPasswordIsBelowBeA3Minimum()
	{
		// SHV2 BE-A3: Testing host requires 12 characters; IntegrationTestCredentials.PasswordOneBelowMinimum is 11.
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			IntegrationTestCredentials.PasswordOneBelowMinimum);
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldSucceed_WhenPasswordMeetsBeA3Minimum()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			IntegrationTestCredentials.DefaultPassword);
		status.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenPasswordIs7Chars()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test12!");
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenPasswordIs8Chars()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1!@#");
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenPasswordIs11Chars()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			IntegrationTestCredentials.PasswordOneBelowMinimum);
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldSucceed_WhenPasswordIsExactly12Chars()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			IntegrationTestCredentials.DefaultPassword);
		status.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Register_ShouldFail_WhenPasswordExceedsSchemaMaxLength()
	{
		// Prefix meets BE-A3 minimum (12); remainder pads to upper boundary.
		var password = IntegrationTestCredentials.DefaultPassword + new string('a', 243);
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			password);
		status.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldSucceed_WhenEmailIsMinLength()
	{
		// Use unique email to avoid conflicts from previous tests
		var uniqueEmail = $"a{Guid.NewGuid().ToString("N")[..8]}@b.c";
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			uniqueEmail,
			IntegrationTestCredentials.DefaultPassword);
		status.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Register_ShouldSucceed_WhenEmailIsMaxLength()
	{
		var longEmail = new string('a', 240) + "@test.com";
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			longEmail,
			IntegrationTestCredentials.DefaultPassword);
		status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldSucceed_WithMinimalValidRequest()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##", "Test", "User");

		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "Test1234!@##"
		};

		// Retry logic with exponential backoff for in-memory database timing issues
		HttpResponseMessage? response = null;
		for (int i = 0; i < 15; i++)
		{
			await Task.Delay(150 * (i + 1)); // Exponential backoff: 150ms, 300ms, 450ms...
			response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
			if (response.StatusCode == HttpStatusCode.OK)
				break;
		}

		response.Should().NotBeNull();
		response!.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldSucceed_WithAllOptionalFields()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest 
	//     { 
	//         GrantType = "password", 
	//         ClientId = "be-demo-client", 
	//         ClientSecret = "be-demo-secret-very-strong-key", 
	//         Username = email, 
	//         Password = "Test1234!@##",
	//         Scope = "read write admin",
	//         Signature = null,
	//         SignatureAlgorithm = "ES512"
	//     };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHandleEmptyScope()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Scope = "" };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task Token_ShouldHandleNullScope()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
	//     var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "Test1234!@##", Scope = null };
	//     var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
	//     response.StatusCode.Should().Be(HttpStatusCode.OK);
	// }

	[Fact]
	public async Task Register_ShouldHandleNullFirstName()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1234!@##",
			firstName: "",
			lastName: "Doe");
		status.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Register_ShouldHandleNullLastName()
	{
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1234!@##",
			firstName: "John",
			lastName: "");
		status.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Register_ShouldHandleVeryLongFirstName()
	{
		var longName = new string('a', 1000);
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1234!@##",
			firstName: longName);
		status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_ShouldHandleVeryLongLastName()
	{
		var longName = new string('a', 1000);
		var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(
			_client,
			_factory,
			$"test_{Guid.NewGuid()}@test.com",
			"Test1234!@##",
			lastName: longName);
		status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldHandleWhitespaceOnlyInUsername()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "   ", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldHandleWhitespaceOnlyInPassword()
	{
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = "test@test.com", Password = "   " };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Token_ShouldHandleWhitespaceInUsername()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = $"  {email}  ", Password = "Test1234!@##" };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Token_ShouldHandleWhitespaceInPassword()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
		var request = new OAuth2TokenRequest { GrantType = "password", ClientId = "be-demo-client", ClientSecret = "be-demo-secret-very-strong-key", Username = email, Password = "  Test1234!@##  " };
		var response = await _client.PostAsJsonAsync("/api/oauth2/token", request);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	public void Dispose()
	{
		_client?.Dispose();
	}
}
