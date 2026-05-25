using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration coverage for <see cref="Controllers.AdminMailerTestController"/> (pilot flow §5.5).
/// </summary>
public sealed class AdminMailerTestControllerTests : IClassFixture<MailDisabledWebApplicationFactory>, IDisposable
{
	private readonly MailDisabledWebApplicationFactory _factory;

	public AdminMailerTestControllerTests(MailDisabledWebApplicationFactory factory) => _factory = factory;

	public void Dispose() { }

	[Fact]
	public async Task TestSelf_ShouldReturnUnauthorized_OnAdminFace_WithoutJwt()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.PostAsync("/api/admin/mailer/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task TestSelf_ShouldReturnBadRequest_WhenMailDisabled_ForSuperAdmin()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.PostAsync("/api/admin/mailer/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("Mail", "error should mention mail worker configuration");
	}

	[Fact]
	public async Task PilotLink_ShouldReturnOk_WithoutAuth()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.GetAsync("/api/admin/mailer/pilot-link");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}
