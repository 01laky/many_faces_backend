using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;

namespace BeDemo.Api.Tests.Validation.Integration;

public sealed class ValidationProblemDetailsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly HttpClient _client;
	private readonly CustomWebApplicationFactory<Program> _factory;

	public ValidationProblemDetailsIntegrationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = factory.CreateUnscopedClient();
	}

	[Fact]
	public async Task Invalid_json_body_returns_problem_details()
	{
		var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");
		var response = await _client.PostAsync("/api/oauth2/register/request", content);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("errors");
	}

	[Fact]
	public async Task Register_request_with_invalid_email_returns_problem_details()
	{
		var response = await _client.PostAsJsonAsync(
			"/api/oauth2/register/request",
			new RegisterRequestDto { Email = "not-an-email" });

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("errors");
	}

	[Fact]
	public async Task OAuth2_token_missing_grant_type_returns_oauth_error_not_problem_details()
	{
		var response = await _client.PostAsJsonAsync(
			"/api/oauth2/token",
			new OAuth2TokenRequest
			{
				GrantType = "",
				ClientId = "be-demo-client",
				ClientSecret = "be-demo-secret-very-strong-key",
			});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("invalid_request");
		body.Should().NotContain("\"errors\"");
	}

	[Fact]
	public async Task Story_image_upload_with_disallowed_content_type_returns_problem_details()
	{
		using var client = _factory.CreateClient();
		var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
			client,
			_factory,
			$"story_val_{Guid.NewGuid():N}@test.com");
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var create = await client.PostAsJsonAsync("/api/stories", new { title = "Validation integration" });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var storyId = created.GetProperty("id").GetInt32();

		using var multipart = new MultipartFormDataContent();
		var bytes = "not-an-image"u8.ToArray();
		var filePart = new ByteArrayContent(bytes);
		filePart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
		multipart.Add(filePart, "file", "note.txt");
		multipart.Add(new StringContent("0"), "sortOrder");

		var upload = await client.PostAsync($"/api/stories/{storyId}/images", multipart);
		upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await upload.Content.ReadAsStringAsync();
		body.Should().Contain("errors");
	}
}
