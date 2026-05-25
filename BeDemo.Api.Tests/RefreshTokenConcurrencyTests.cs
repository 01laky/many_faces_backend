using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Concurrent refresh with the same plaintext: exactly one request may rotate successfully (single-use A17).
/// In-memory path uses a process-wide lock in <see cref="BeDemo.Api.Services.OAuthRefreshTokenStore"/> for parity with PostgreSQL SERIALIZABLE.
/// </summary>
public sealed class RefreshTokenConcurrencyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public RefreshTokenConcurrencyTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task ParallelRefresh_WithSamePlaintext_ExactlyOneSucceeds()
	{
		using var client = _factory.CreateUnscopedClient();
		var email = $"conc_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(client, _factory, email, "Test1234!@##");
		var tokenReq = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = "Test1234!@##",
		};
		var first = await client.PostAsJsonAsync("/api/oauth2/token", tokenReq);
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await first.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		var refresh = body!.RefreshToken!;

		var refreshReq = new OAuth2TokenRequest
		{
			GrantType = "refresh_token",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			RefreshToken = refresh,
		};

		var taskA = client.PostAsJsonAsync("/api/oauth2/token", refreshReq);
		var taskB = client.PostAsJsonAsync("/api/oauth2/token", refreshReq);

		var results = await Task.WhenAll(taskA, taskB);
		var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
		var unauthorizedCount = results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

		okCount.Should().Be(1);
		unauthorizedCount.Should().Be(1);
	}
}
