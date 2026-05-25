using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using BeDemo.Api.Services.OperatorPush;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Integration coverage for <see cref="Controllers.AdminPushSettingsController"/> (APC-B*).</summary>
public sealed class AdminPushSettingsControllerTests
	: IClassFixture<CustomWebApplicationFactory<Program>>,
		IClassFixture<PushDisabledWebApplicationFactory>,
		IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly PushDisabledWebApplicationFactory _pushDisabledFactory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _adminFace;

	public AdminPushSettingsControllerTests(
		CustomWebApplicationFactory<Program> factory,
		PushDisabledWebApplicationFactory pushDisabledFactory)
	{
		_factory = factory;
		_pushDisabledFactory = pushDisabledFactory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
	}

	public void Dispose()
	{
		IntegrationTestPush.ResetToBootstrapAsync(_factory).GetAwaiter().GetResult();
	}

	[Fact]
	public async Task APC_B1_GetSettings_ReturnsBootstrap_WhenNoPriorPut()
	{
		var client = await CreateSuperAdminClientAsync();
		var res = await client.GetAsync("/api/admin/push/settings");
		res.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await res.Content.ReadFromJsonAsync<AdminPushSettingsDto>();
		dto.Should().NotBeNull();
		dto!.WorkerGrpcUrl.Should().Contain("59997");
		dto.Firebase.HasCredentials.Should().BeTrue();
	}

	[Fact]
	public async Task APC_B2_PutSettings_PersistsEnabledPlatformAndFirebase()
	{
		var client = await CreateSuperAdminClientAsync();
		var body = ValidPutBody(enabled: true);
		var res = await client.PutAsJsonAsync("/api/admin/push/settings", body);
		res.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await res.Content.ReadFromJsonAsync<AdminPushSettingsDto>();
		dto!.Enabled.Should().BeTrue();
		dto.WorkerGrpcUrl.Should().Be("http://push-worker.test:50053");
		dto.Firebase.ProjectId.Should().Be("demo-project");
		dto.Defaults.TitleLocKey.Should().Be("push_test_title");
	}

	[Fact]
	public async Task APC_B3_PutSettings_InvalidWorkerUrl_Returns400()
	{
		var client = await CreateSuperAdminClientAsync();
		var body = ValidPutBody();
		body.WorkerGrpcUrl = "not-a-url";
		var res = await client.PutAsJsonAsync("/api/admin/push/settings", body);
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task APC_B4_PutSettings_InvalidServiceAccountJson_Returns400()
	{
		var client = await CreateSuperAdminClientAsync();
		var body = ValidPutBody();
		body.Firebase!.ServiceAccountJson = "{not-json";
		var res = await client.PutAsJsonAsync("/api/admin/push/settings", body);
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task APC_B5_GetSettings_NeverReturnsSecrets()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody();
		put.WorkerAuthToken = "super-secret-token";
		put.Firebase!.ServiceAccountJson = IntegrationTestPush.TestFirebaseServiceAccountJson;
		(await client.PutAsJsonAsync("/api/admin/push/settings", put)).EnsureSuccessStatusCode();

		var json = await (await client.GetAsync("/api/admin/push/settings")).Content.ReadAsStringAsync();
		json.Should().NotContain("super-secret-token");
		json.Should().NotContain("private_key");
		json.Should().NotContain("serviceAccountJson");
	}

	[Fact]
	public async Task APC_B6_PutRotateWorkerToken_HasFlagWithoutPlaintext()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody();
		put.WorkerAuthToken = "rotate-me-token";
		(await client.PutAsJsonAsync("/api/admin/push/settings", put)).EnsureSuccessStatusCode();

		var dto = await (await client.GetAsync("/api/admin/push/settings")).Content.ReadFromJsonAsync<AdminPushSettingsDto>();
		dto!.HasWorkerAuthToken.Should().BeTrue();
		var json = await (await client.GetAsync("/api/admin/push/settings")).Content.ReadAsStringAsync();
		json.Should().NotContain("rotate-me-token");
	}

	[Fact]
	public async Task APC_B7_GetSettings_Returns401WithoutJwt()
	{
		var res = await _adminFace.GetAsync("/api/admin/push/settings");
		res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task APC_B7b_GetSettings_Returns403ForGlobalAdmin()
	{
		var client = AclTestClients.CreateAdminFaceClient(_factory);
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(_oauth);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var res = await client.GetAsync("/api/admin/push/settings");
		res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task APC_B8_PutDisabled_SkipsPushSend()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody(enabled: false);
		(await client.PutAsJsonAsync("/api/admin/push/settings", put)).EnsureSuccessStatusCode();

		_factory.CapturingPush.Reset();
		(await client.PostAsync("/api/admin/push/test-self", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		_factory.CapturingPush.LastRequest.Should().BeNull();
	}

	[Fact]
	public async Task APC_B9_TestSelf_RespectsDisabledFlag()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody(enabled: false);
		(await client.PutAsJsonAsync("/api/admin/push/settings", put)).EnsureSuccessStatusCode();
		(await client.PostAsync("/api/admin/push/test-self", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task APC_B10_WorkerConfig_ReflectsEffectiveStatus()
	{
		var client = await CreateSuperAdminClientAsync();
		(await client.PutAsJsonAsync("/api/admin/push/settings", ValidPutBody())).EnsureSuccessStatusCode();

		var infra = await (await client.GetAsync("/api/admin/infra/worker-config")).Content.ReadFromJsonAsync<JsonElement>();
		infra.GetProperty("push").GetProperty("configured").GetBoolean().Should().BeTrue();
		infra.GetProperty("push").GetProperty("effectiveStatus").GetString().Should().Be("configured");
	}

	[Fact]
	public async Task APC_B11_PutEnabledWithoutCredentials_Returns400()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody();
		put.Firebase!.ServiceAccountJson = "";
		var res = await client.PutAsJsonAsync("/api/admin/push/settings", put);
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task APC_B12_PushClient_IncludesFcmBlockOnSend()
	{
		var client = await CreateSuperAdminClientAsync();
		(await client.PutAsJsonAsync("/api/admin/push/settings", ValidPutBody())).EnsureSuccessStatusCode();

		_factory.CapturingPush.Reset();
		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var superRole = await db.UserRoles.AsNoTracking()
			.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
		var user = await db.Users.FirstAsync(u => u.UserRoleId == superRole.Id);
		db.UserPushDevices.Add(new UserPushDevice
		{
			UserId = user.Id,
			Platform = "android",
			RegistrationToken = "test-token-apc-b12",
			CreatedAtUtc = DateTime.UtcNow,
			UpdatedAtUtc = DateTime.UtcNow,
		});
		await db.SaveChangesAsync();

		(await client.PostAsync("/api/admin/push/test-self", null)).EnsureSuccessStatusCode();
		_factory.CapturingPush.LastRequest.Should().NotBeNull();
		_factory.CapturingPush.LastRequest!.Fcm.Should().NotBeNull();
		_factory.CapturingPush.LastRequest.Fcm!.ServiceAccountJson.Should().Contain("service_account");
	}

	[Fact]
	public async Task APC_B13_PutRotateFirebaseJson_HasCredentialsWithoutPlaintext()
	{
		var client = await CreateSuperAdminClientAsync();
		var put = ValidPutBody();
		put.Firebase!.ServiceAccountJson = IntegrationTestPush.TestFirebaseServiceAccountJson;
		(await client.PutAsJsonAsync("/api/admin/push/settings", put)).EnsureSuccessStatusCode();

		var dto = await (await client.GetAsync("/api/admin/push/settings")).Content.ReadFromJsonAsync<AdminPushSettingsDto>();
		dto!.Firebase.HasCredentials.Should().BeTrue();
		var json = await (await client.GetAsync("/api/admin/push/settings")).Content.ReadAsStringAsync();
		json.Should().NotContain("private_key");
	}

	[Fact]
	public async Task APC_B14_DirectSendPath_AttachesFcmBlock()
	{
		var client = await CreateSuperAdminClientAsync();
		(await client.PutAsJsonAsync("/api/admin/push/settings", ValidPutBody())).EnsureSuccessStatusCode();

		_factory.CapturingPush.Reset();
		await using var scope = _factory.Services.CreateAsyncScope();
		var pushClient = scope.ServiceProvider.GetRequiredService<IPushWorkerClient>();
		var req = new ManyFaces.Push.V1.SendPushRequest
		{
			TitleLocKey = "k",
			BodyLocKey = "b",
		};
		req.RegistrationTokens.Add("tok-direct");
		(await pushClient.SendPushAsync(req)).Should().NotBeNull();
		_factory.CapturingPush.LastRequest.Should().NotBeNull();
		_factory.CapturingPush.LastRequest!.Fcm.Should().NotBeNull();
	}

	private static UpdateAdminPushSettingsRequest ValidPutBody(bool enabled = true) => new()
	{
		Enabled = enabled,
		WorkerGrpcUrl = "http://push-worker.test:50053",
		Defaults = new UpdateAdminPushDefaultsRequest
		{
			TitleLocKey = "push_test_title",
			BodyLocKey = "push_test_body",
			AndroidChannelId = "default",
		},
		Firebase = new UpdateAdminPushFirebaseRequest
		{
			ServiceAccountJson = IntegrationTestPush.TestFirebaseServiceAccountJson,
		},
		GrpcDeadlineSeconds = 15,
	};

	private async Task<HttpClient> CreateSuperAdminClientAsync(CustomWebApplicationFactory<Program>? factory = null)
	{
		factory ??= _factory;
		await RestoreSuperAdminInFactoryAsync(factory);
		var client = AclTestClients.CreateAdminFaceClient(factory);
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client;
	}

	private static async Task RestoreSuperAdminInFactoryAsync(CustomWebApplicationFactory<Program> factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
		var superRole = await db.UserRoles.AsNoTracking()
			.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
		var user = await db.Users.FirstAsync(u => u.UserRoleId == superRole.Id);
		user.Email = IntegrationTestSeed.SuperAdminEmail;
		user.UserName = IntegrationTestSeed.SuperAdminEmail;
		user.NormalizedEmail = userManager.NormalizeEmail(user.Email);
		user.NormalizedUserName = userManager.NormalizeName(user.UserName);
		user.EmailConfirmed = true;
		await userManager.UpdateAsync(user);
		var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
		await userManager.ResetPasswordAsync(user, resetToken, IntegrationTestSeed.Password);
		factory.Services.GetRequiredService<IOperatorMailSettingsProvider>().InvalidateCache();
		factory.Services.GetRequiredService<IOperatorPushSettingsProvider>().InvalidateCache();
	}
}
