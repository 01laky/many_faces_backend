using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class OperatorAiSystemSettingsIntegrationTests : IClassFixture<OperatorAiGrpcMockWebApplicationFactory>, IDisposable
{
	private readonly OperatorAiGrpcMockWebApplicationFactory _factory;

	public OperatorAiSystemSettingsIntegrationTests(OperatorAiGrpcMockWebApplicationFactory factory) =>
		_factory = factory;

	public void Dispose()
	{
		_factory.Ai.ModelStatusHandler = null;
		_factory.Ai.ResetModelStatusPollCount();
		IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, true).GetAwaiter().GetResult();
	}

	private async Task<HttpClient> CreateAdminClientAsync()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client;
	}

	[Fact]
	public async Task Get_system_settings_returns_enabled_for_test_harness()
	{
		var client = await CreateAdminClientAsync();
		var response = await client.GetAsync("/api/operator-ai/system-settings");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<OperatorAiSystemSettingsDto>();
		dto!.AiEnabled.Should().BeTrue();
	}

	[Fact]
	public async Task Put_disable_persists_without_health_probe()
	{
		_factory.Ai.ResetModelStatusPollCount();
		var client = await CreateAdminClientAsync();
		var put = await client.PutAsJsonAsync("/api/operator-ai/system-settings", new { aiEnabled = false });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await put.Content.ReadFromJsonAsync<OperatorAiSystemSettingsDto>();
		dto!.AiEnabled.Should().BeFalse();
		_factory.Ai.ModelStatusPollCount.Should().Be(0);
	}

	[Fact]
	public async Task Put_enable_when_ready_persists_true()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		_factory.Ai.ModelStatusHandler = () => new AiModelStatus(true, false, false, "test-model");

		var client = await CreateAdminClientAsync();
		var put = await client.PutAsJsonAsync("/api/operator-ai/system-settings", new { aiEnabled = true });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await put.Content.ReadFromJsonAsync<OperatorAiSystemSettingsDto>();
		dto!.AiEnabled.Should().BeTrue();
		_factory.Ai.ModelStatusPollCount.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task Put_enable_when_unavailable_returns_503_and_keeps_false()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		_factory.Ai.ModelStatusHandler = () => new AiModelStatus(false, false, true, null);

		var client = await CreateAdminClientAsync();
		var put = await client.PutAsJsonAsync("/api/operator-ai/system-settings", new { aiEnabled = true });
		put.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

		var get = await client.GetAsync("/api/operator-ai/system-settings");
		var dto = await get.Content.ReadFromJsonAsync<OperatorAiSystemSettingsDto>();
		dto!.AiEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task Put_enable_loading_until_timeout_returns_503()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		_factory.Ai.ModelStatusHandler = () => new AiModelStatus(false, true, false, "loading-model");

		var client = await CreateAdminClientAsync();

		var put = await client.PutAsJsonAsync("/api/operator-ai/system-settings", new { aiEnabled = true });
		put.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

		var body = await put.Content.ReadFromJsonAsync<Dictionary<string, string>>();
		body!["errorCode"].Should().Be(OperatorAiEnableService.ErrorModelLoadingTimeout);
	}

	[Fact]
	public async Task Post_conversation_when_ai_disabled_returns_409()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		var client = await CreateAdminClientAsync();

		var create = await client.PostAsJsonAsync("/api/operator-ai/conversations", new { title = "Blocked" });
		create.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Put_public_stats_when_ai_disabled_returns_409()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		var client = await CreateAdminClientAsync();

		var put = await client.PutAsJsonAsync(
			"/api/operator-ai/public-stats-settings",
			new { publicStatsMode = "live", liveMaxParallelBundleCalls = 2 });
		put.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Get_ai_enabled_anonymous_on_public_face()
	{
		await IntegrationTestSeed.SetOperatorAiEnabledAsync(_factory.Services, false);
		var client = _factory.CreateFaceClient("public");
		var response = await client.GetAsync("/api/ai/enabled");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var payload = await response.Content.ReadFromJsonAsync<AiEnabledProbeDto>();
		payload!.Enabled.Should().BeFalse();
	}

	private sealed class AiEnabledProbeDto
	{
		public bool Enabled { get; set; }
	}
}
