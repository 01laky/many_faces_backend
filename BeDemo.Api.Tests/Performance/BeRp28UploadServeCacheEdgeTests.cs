using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP28 edge cases (BE-RP28-U1…U5).</summary>
[Trait("Category", "BackendSecurity")]
public sealed class BeRp28UploadServeCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;
	private readonly HttpClient _anonymous;

	public BeRp28UploadServeCacheEdgeTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = factory.CreateClient();
		_anonymous = factory.CreateUnscopedClient();
	}

	public void Dispose()
	{
		_client.Dispose();
		_anonymous.Dispose();
	}

	private async Task<string> UploadTinyAvatarAndGetSignedUrlAsync()
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
			_client,
			_factory,
			$"upload_cache_{Guid.NewGuid():N}@test.com");
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		using var multipart = new MultipartFormDataContent();
		var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
		var filePart = new ByteArrayContent(png);
		filePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		multipart.Add(filePart, "file", "tiny.png");

		var upload = await _client.PostAsync("/api/profile/me/avatar", multipart);
		upload.EnsureSuccessStatusCode();
		var payload = await upload.Content.ReadFromJsonAsync<JsonElement>();
		return payload.GetProperty("avatarUrl").GetString()!;
	}

	/// <summary>BE-RP28-U1 — valid sig returns 200 with Cache-Control private max-age.</summary>
	[Fact]
	public async Task BE_RP28_U1_ValidSig_ReturnsCacheControl()
	{
		var signedUrl = await UploadTinyAvatarAndGetSignedUrlAsync();
		var response = await _anonymous.GetAsync(signedUrl);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Headers.CacheControl.Should().NotBeNull();
		response.Headers.CacheControl!.Private.Should().BeTrue();
		response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.Zero);
		response.Headers.ETag.Should().NotBeNull();
		response.Headers.ETag!.Tag.Should().NotBeNullOrEmpty();
	}

	/// <summary>BE-RP28-U2 — expired sig returns 401 without cache headers.</summary>
	[Fact]
	public async Task BE_RP28_U2_ExpiredSig_UnauthorizedWithoutCacheHeaders()
	{
		using var scope = _factory.Services.CreateScope();
		var signer = scope.ServiceProvider.GetRequiredService<IUploadSignedUrlService>();
		var path = UploadPathSecurity.BuildUploadUrlPath("uploads", "avatars", "probe", "x.png");
		var servePath = signer.ToSignedServePath(path);
		servePath.Should().NotBeNull();

		var tampered = new UriBuilder($"{_anonymous.BaseAddress}{servePath!.TrimStart('/')}")
		{
			Query = "path=/uploads/x.png&exp=1&sig=invalid",
		}.Uri;

		var response = await _anonymous.GetAsync(tampered);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		response.Headers.CacheControl.Should().BeNull();
	}

	/// <summary>BE-RP28-U3 — repeat request with If-None-Match returns 304.</summary>
	[Fact]
	public async Task BE_RP28_U3_RepeatWithIfNoneMatch_Returns304()
	{
		var signedUrl = await UploadTinyAvatarAndGetSignedUrlAsync();
		var first = await _anonymous.GetAsync(signedUrl);
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		var etag = first.Headers.ETag!.Tag;

		var req = new HttpRequestMessage(HttpMethod.Get, signedUrl);
		req.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));
		var second = await _anonymous.SendAsync(req);
		second.StatusCode.Should().Be(HttpStatusCode.NotModified);
	}

	/// <summary>BE-RP28-U4 — range request still returns partial content.</summary>
	[Fact]
	public async Task BE_RP28_U4_RangeRequest_ReturnsPartialContent()
	{
		var signedUrl = await UploadTinyAvatarAndGetSignedUrlAsync();
		var req = new HttpRequestMessage(HttpMethod.Get, signedUrl);
		req.Headers.Range = new RangeHeaderValue(0, 3);
		var response = await _anonymous.SendAsync(req);
		response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
		var bytes = await response.Content.ReadAsByteArrayAsync();
		bytes.Should().HaveCount(4);
	}

	/// <summary>BE-RP28-U5 — path traversal attempt remains rejected.</summary>
	[Fact]
	public async Task BE_RP28_U5_PathTraversal_StillRejected()
	{
		var tampered = new UriBuilder($"{_anonymous.BaseAddress}api/uploads/serve")
		{
			Query = "path=/uploads/avatars/../../etc/passwd&exp=9999999999&sig=invalid",
		}.Uri;
		var response = await _anonymous.GetAsync(tampered);
		response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
	}
}
