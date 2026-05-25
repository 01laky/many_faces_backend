using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration tests for password-grant <c>rememberMe</c> → JWT lifetime (Jwt:ExpiresInMinutes vs Jwt:ExpiresInMinutesRememberMe).
/// Uses per-client configuration overrides so assertions stay small and stable.
/// </summary>
public class OAuth2RememberMeTests
{
	private sealed class JwtDurationFactory : CustomWebApplicationFactory<Program>
	{
		private readonly int _sessionMinutes;
		private readonly int _rememberMinutes;

		public JwtDurationFactory(int sessionMinutes, int rememberMinutes)
		{
			_sessionMinutes = sessionMinutes;
			_rememberMinutes = rememberMinutes;
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			builder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Jwt:ExpiresInMinutes"] = _sessionMinutes.ToString(),
					["Jwt:ExpiresInMinutesRememberMe"] = _rememberMinutes.ToString(),
				});
			});
		}
	}

	private static async Task<string> RegisterUniqueUserAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory)
	{
		var email = $"remember_{Guid.NewGuid():N}@test.com";
		await IntegrationTestRegistration.CompleteRegistrationAsync(client, factory, email, "Test1234!@##", "R", "M");
		return email;
	}

	private static async Task<OAuth2TokenResponse?> LoginAsync(
		HttpClient client,
		string email,
		string password,
		bool? rememberMe)
	{
		var body = new Dictionary<string, object?>
		{
			["grantType"] = "password",
			["clientId"] = "be-demo-client",
			["clientSecret"] = "be-demo-secret-very-strong-key",
			["username"] = email,
			["password"] = password
		};
		if (rememberMe.HasValue)
			body["rememberMe"] = rememberMe.Value;

		var response = await client.PostAsJsonAsync("/api/oauth2/token", body);
		if (response.StatusCode != HttpStatusCode.OK)
			return null;
		return await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
	}

	[Fact]
	public async Task Token_WithoutRememberMe_UsesSessionExpiry()
	{
		const int sessionMin = 13;
		const int rememberMin = 777;
		using var factory = new JwtDurationFactory(sessionMin, rememberMin);
		using var client = factory.CreateClient();
		var email = await RegisterUniqueUserAsync(client, factory);

		var token = await LoginAsync(client, email, "Test1234!@##", rememberMe: null);

		token.Should().NotBeNull();
		token!.ExpiresIn.Should().Be(sessionMin * 60);
	}

	[Fact]
	public async Task Token_WithRememberMeFalse_UsesSessionExpiry()
	{
		const int sessionMin = 14;
		const int rememberMin = 888;
		using var factory = new JwtDurationFactory(sessionMin, rememberMin);
		using var client = factory.CreateClient();
		var email = await RegisterUniqueUserAsync(client, factory);

		var token = await LoginAsync(client, email, "Test1234!@##", rememberMe: false);

		token.Should().NotBeNull();
		token!.ExpiresIn.Should().Be(sessionMin * 60);
	}

	[Fact]
	public async Task Token_WithRememberMeTrue_UsesRememberExpiry()
	{
		const int sessionMin = 15;
		const int rememberMin = 999;
		using var factory = new JwtDurationFactory(sessionMin, rememberMin);
		using var client = factory.CreateClient();
		var email = await RegisterUniqueUserAsync(client, factory);

		var token = await LoginAsync(client, email, "Test1234!@##", rememberMe: true);

		token.Should().NotBeNull();
		token!.ExpiresIn.Should().Be(rememberMin * 60);
	}

	[Fact]
	public async Task Default_appsettings_remember_me_uses_seven_day_access_token()
	{
		using var factory = new CustomWebApplicationFactory<Program>();
		using var client = factory.CreateClient();
		var email = await RegisterUniqueUserAsync(client, factory);

		var token = await LoginAsync(client, email, "Test1234!@##", rememberMe: true);

		token.Should().NotBeNull();
		token!.ExpiresIn.Should().Be(JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes * 60);
	}

	[Fact]
	public async Task Token_RememberMeTrue_StillAuthenticates_AfterRetry()
	{
		const int sessionMin = 3;
		const int rememberMin = 400;
		using var factory = new JwtDurationFactory(sessionMin, rememberMin);
		using var client = factory.CreateClient();
		var email = await RegisterUniqueUserAsync(client, factory);

		OAuth2TokenResponse? token = null;
		for (var i = 0; i < 15; i++)
		{
			await Task.Delay(150 * (i + 1));
			token = await LoginAsync(client, email, "Test1234!@##", rememberMe: true);
			if (token != null)
				break;
		}

		token.Should().NotBeNull();
		token!.ExpiresIn.Should().Be(rememberMin * 60);
		token.AccessToken.Should().NotBeNullOrEmpty();
	}
}
