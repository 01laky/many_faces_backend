using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Tests;

/// <summary>Email-code registration flow for integration tests (request → mail capture → complete).</summary>
internal static class IntegrationTestRegistration
{
	private static readonly object FlowLock = new();

	public static async Task<RegisterCompleteResponseDto> CompleteRegistrationAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string email,
		string password,
		string firstName = "Test",
		string lastName = "User",
		string locale = "en")
	{
		lock (FlowLock)
		{
			return CompleteRegistrationCoreAsync(client, factory, email, password, firstName, lastName, locale)
				.GetAwaiter()
				.GetResult();
		}
	}

	public static async Task<string> RegisterAndGetAccessTokenAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string? email = null,
		string password = IntegrationTestCredentials.DefaultPassword,
		string firstName = "Test",
		string lastName = "User")
	{
		email ??= $"test_{Guid.NewGuid():N}@test.com";
		var body = await CompleteRegistrationAsync(client, factory, email, password, firstName, lastName);
		body.AccessToken.Should().NotBeNullOrEmpty();
		return body.AccessToken!;
	}

	/// <summary>POST <c>/register/request</c> only; returns HTTP status (no mail capture required for negative cases).</summary>
	public static async Task<HttpStatusCode> TryRegisterRequestAsync(
		HttpClient client,
		string email,
		string firstName = "Test",
		string lastName = "User",
		string locale = "en")
	{
		var response = await client.PostAsJsonAsync(
			"/api/oauth2/register/request",
			new RegisterRequestDto
			{
				Email = email,
				FirstName = firstName,
				LastName = lastName,
				Locale = locale,
			});
		return response.StatusCode;
	}

	/// <summary>Runs invite flow without FluentAssertions; returns final HTTP status (request or complete).</summary>
	public static async Task<HttpStatusCode> TryCompleteRegistrationAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string email,
		string password,
		string firstName = "Test",
		string lastName = "User",
		string locale = "en")
	{
		lock (FlowLock)
		{
			return TryCompleteRegistrationCoreAsync(
					client,
					factory,
					email,
					password,
					firstName,
					lastName,
					locale)
				.GetAwaiter()
				.GetResult();
		}
	}

	public static async Task<(string AccessToken, string UserId, string Email)> RegisterLoginWithUserIdAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string? email = null,
		string password = IntegrationTestCredentials.DefaultPassword,
		string firstName = "Test",
		string lastName = "User")
	{
		email ??= $"user_{Guid.NewGuid():N}@test.com";
		var accessToken = await RegisterAndGetAccessTokenViaPasswordGrantAsync(
			client,
			factory,
			email,
			password,
			firstName,
			lastName);
		return (accessToken, ParseUserIdFromAccessToken(accessToken), email);
	}

	public static string ParseUserIdFromAccessToken(string accessToken)
	{
		var payload = accessToken.Split('.')[1];
		var pad = payload.Length % 4 == 0 ? "" : new string('=', 4 - payload.Length % 4);
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload + pad));
		using var doc = JsonDocument.Parse(json);
		var userId = doc.RootElement.TryGetProperty("nameid", out var n)
			? n.GetString()
			: doc.RootElement.GetProperty("sub").GetString();
		userId.Should().NotBeNullOrEmpty();
		return userId!;
	}

	public static async Task<string> RegisterAndGetAccessTokenViaPasswordGrantAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string? email = null,
		string password = IntegrationTestCredentials.DefaultPassword,
		string firstName = "Test",
		string lastName = "User")
	{
		email ??= $"test_{Guid.NewGuid():N}@test.com";
		await CompleteRegistrationAsync(client, factory, email, password, firstName, lastName);

		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = password,
		};

		HttpResponseMessage? response = null;
		for (var i = 0; i < 15; i++)
		{
			await Task.Delay(150 * (i + 1));
			response = await client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
			if (response.StatusCode == HttpStatusCode.OK)
				break;
		}

		response.Should().NotBeNull();
		response!.StatusCode.Should().Be(HttpStatusCode.OK);
		var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
		tokenResponse.Should().NotBeNull();
		return tokenResponse!.AccessToken!;
	}

	private static async Task<HttpStatusCode> TryCompleteRegistrationCoreAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string email,
		string password,
		string firstName,
		string lastName,
		string locale)
	{
		await IntegrationTestMail.ResetToBootstrapAsync(factory);
		factory.CapturingMailer.Reset();
		var requestResponse = await client.PostAsJsonAsync(
			"/api/oauth2/register/request",
			new RegisterRequestDto
			{
				Email = email,
				FirstName = firstName,
				LastName = lastName,
				Locale = locale,
			});
		if (!requestResponse.IsSuccessStatusCode)
			return requestResponse.StatusCode;

		if (factory.CapturingMailer.LastRequest is null)
			return HttpStatusCode.InternalServerError;

		var code = factory.CapturingMailer.LastRequest.Params["registration_code"];

		using var scope = factory.Services.CreateScope();
		var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).FirstOrDefault(i => i.Email == email);
		if (invite is null)
			return HttpStatusCode.BadRequest;

		var completeResponse = await client.PostAsJsonAsync(
			"/api/oauth2/register/complete",
			new RegisterCompleteDto
			{
				Hash = invite.LinkHash,
				Code = code,
				Password = password,
				FirstName = firstName,
				LastName = lastName,
				ClientId = "be-demo-client",
				ClientSecret = "be-demo-secret-very-strong-key",
			});
		return completeResponse.StatusCode;
	}

	private static async Task<RegisterCompleteResponseDto> CompleteRegistrationCoreAsync(
		HttpClient client,
		CustomWebApplicationFactory<Program> factory,
		string email,
		string password,
		string firstName,
		string lastName,
		string locale)
	{
		await IntegrationTestMail.ResetToBootstrapAsync(factory);
		factory.CapturingMailer.Reset();
		var requestResponse = await client.PostAsJsonAsync(
			"/api/oauth2/register/request",
			new RegisterRequestDto
			{
				Email = email,
				FirstName = firstName,
				LastName = lastName,
				Locale = locale,
			});
		requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		factory.CapturingMailer.LastRequest.Should().NotBeNull();
		var code = factory.CapturingMailer.LastRequest!.Params["registration_code"];

		using var scope = factory.Services.CreateScope();
		var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).First(i => i.Email == email);

		var completeResponse = await client.PostAsJsonAsync(
			"/api/oauth2/register/complete",
			new RegisterCompleteDto
			{
				Hash = invite.LinkHash,
				Code = code,
				Password = password,
				FirstName = firstName,
				LastName = lastName,
				ClientId = "be-demo-client",
				ClientSecret = "be-demo-secret-very-strong-key",
			});
		completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await completeResponse.Content.ReadFromJsonAsync<RegisterCompleteResponseDto>();
		body.Should().NotBeNull();
		return body!;
	}
}
