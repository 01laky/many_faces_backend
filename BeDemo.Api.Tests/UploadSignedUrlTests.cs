using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>SHV2 BE-U3 — HMAC-signed upload serve URLs and blocked anonymous /uploads/* static files.</summary>
/// <summary>SHV2 BE-U3 — HMAC signed upload serve URLs.</summary>
[Trait("Category", "BackendSecurity")]
public sealed class UploadSignedUrlTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public UploadSignedUrlTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = factory.CreateClient();
	}

	public void Dispose() => _client.Dispose();

	[Fact]
	public async Task Direct_uploads_static_path_is_not_publicly_served()
	{
		// Unscoped: no face prefix — may be 403 (face middleware) or 404 (static file deny).
		var response = await _factory.CreateUnscopedClient().GetAsync("/uploads/avatars/probe/secret.jpg");
		response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Signed_serve_url_returns_file_after_avatar_upload()
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
			_client,
			_factory,
			$"signed_avatar_{Guid.NewGuid():N}@test.com");
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		using var multipart = new MultipartFormDataContent();
		var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
		var filePart = new ByteArrayContent(png);
		filePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		multipart.Add(filePart, "file", "tiny.png");

		var upload = await _client.PostAsync("/api/profile/me/avatar", multipart);
		upload.StatusCode.Should().Be(HttpStatusCode.OK);
		var payload = await upload.Content.ReadFromJsonAsync<JsonElement>();
		var signedUrl = payload.GetProperty("avatarUrl").GetString();
		signedUrl.Should().Contain("/api/uploads/serve");
		signedUrl.Should().Contain("sig=");

		var download = await _client.GetAsync(signedUrl);
		download.StatusCode.Should().Be(HttpStatusCode.OK);
		download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

		var storedPath = UploadPathSecurity.BuildUploadUrlPath("uploads", "avatars", userId, "global.png");
		using var scope = _factory.Services.CreateScope();
		var signer = scope.ServiceProvider.GetRequiredService<IUploadSignedUrlService>();
		var tamperedUri = new UriBuilder(signedUrl!) { Query = "path=/uploads/x&exp=9999999999&sig=invalid" }.Uri;
		var tampered = await _factory.CreateUnscopedClient().GetAsync(tamperedUri);
		tampered.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		signer.ToSignedServePath(storedPath).Should().NotBeNull();
	}
}
