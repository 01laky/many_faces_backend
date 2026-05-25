using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiConversationsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public OperatorAiConversationsControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	public void Dispose() { }

	[Fact]
	public async Task List_returns_Forbidden_on_public_face_scope()
	{
		var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/conversations");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Crud_roundtrip_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var create = await client.PostAsJsonAsync("/api/operator-ai/conversations", new { title = "Support thread" });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<OperatorAiConversationListItemDto>();
		created!.Title.Should().Be("Support thread");

		var list = await client.GetAsync("/api/operator-ai/conversations");
		list.StatusCode.Should().Be(HttpStatusCode.OK);
		var items = await list.Content.ReadFromJsonAsync<List<OperatorAiConversationListItemDto>>();
		items!.Should().ContainSingle(c => c.Id == created.Id);

		var messages = await client.GetAsync($"/api/operator-ai/conversations/{created.Id}/messages");
		messages.StatusCode.Should().Be(HttpStatusCode.OK);

		var delete = await client.DeleteAsync($"/api/operator-ai/conversations/{created.Id}");
		delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task Model_status_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/model-status");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var status = await response.Content.ReadFromJsonAsync<OperatorAiModelStatusDto>();
		status.Should().NotBeNull();
	}

	[Fact]
	public async Task Worker_host_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/worker-host");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<OperatorAiWorkerHostDto>();
		payload.Should().NotBeNull();
	}

	[Fact]
	public async Task Worker_host_returns_Forbidden_on_public_face_scope()
	{
		var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/worker-host");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Live_stats_cache_get_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/live-stats-cache");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<OperatorAiLiveStatsCacheSettingsDto>();
		payload.Should().NotBeNull();
		payload!.TtlMilliseconds.Should().BeInRange(30_000, 3_600_000);
	}

	[Fact]
	public async Task Live_stats_cache_put_roundtrip_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var put = await client.PutAsJsonAsync(
			"/api/operator-ai/live-stats-cache",
			new { ttlMilliseconds = 420_000L });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var saved = await put.Content.ReadFromJsonAsync<OperatorAiLiveStatsCacheSettingsDto>();
		saved!.TtlMilliseconds.Should().Be(420_000);
	}

	[Fact]
	public async Task Public_stats_settings_get_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/public-stats-settings");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<OperatorAiPublicStatsSettingsDto>();
		payload.Should().NotBeNull();
		payload!.PublicStatsMode.Should().BeOneOf("off", "inline", "live");
		payload.LiveMaxParallelBundleCalls.Should().BeInRange(1, 8);
	}

	[Fact]
	public async Task Public_stats_settings_put_roundtrip_on_admin_face_scope()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var put = await client.PutAsJsonAsync(
			"/api/operator-ai/public-stats-settings",
			new { publicStatsMode = "live", liveMaxParallelBundleCalls = 4 });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var saved = await put.Content.ReadFromJsonAsync<OperatorAiPublicStatsSettingsDto>();
		saved!.PublicStatsMode.Should().Be("live");
		saved.LiveMaxParallelBundleCalls.Should().Be(4);
	}

	[Fact]
	public async Task Public_stats_settings_returns_Forbidden_on_public_face_scope()
	{
		var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/public-stats-settings");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Live_stats_cache_returns_Forbidden_on_public_face_scope()
	{
		var client = _factory.CreateClient();
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/operator-ai/live-stats-cache");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}
}
