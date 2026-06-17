using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using Xunit;

namespace BeDemo.Api.Tests.Services;

public sealed class AiWorkerHostProfileServiceTests
{
	private static ApplicationDbContext CreateDb()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	[Fact]
	public async Task RefreshFromWorkerAsync_creates_profile_on_first_success()
	{
		await using var db = CreateDb();
		var ai = new FakeAiGrpcService
		{
			HostProfileJson = SampleProfileJson("worker-a"),
		};
		var svc = CreateService(db, ai);

		await svc.RefreshFromWorkerAsync();

		var row = await db.AiWorkerHostProfiles.SingleAsync();
		row.WorkerInstanceId.Should().Be("sha256:worker-a");
		row.Hostname.Should().Be("DESKTOP-TEST");
		var meta = await db.AiWorkerHostRefreshMetas.SingleAsync();
		meta.LastRefreshSucceeded.Should().BeTrue();
	}

	[Fact]
	public async Task RefreshFromWorkerAsync_updates_same_worker_instance()
	{
		await using var db = CreateDb();
		var ai = new FakeAiGrpcService
		{
			HostProfileJson = SampleProfileJson("worker-a", hostname: "HOST-1"),
		};
		var svc = CreateService(db, ai);
		await svc.RefreshFromWorkerAsync();

		ai.HostProfileJson = SampleProfileJson("worker-a", hostname: "HOST-2");
		await svc.RefreshFromWorkerAsync();

		var rows = await db.AiWorkerHostProfiles.ToListAsync();
		rows.Should().ContainSingle();
		rows[0].Hostname.Should().Be("HOST-2");
	}

	[Fact]
	public async Task RefreshFromWorkerAsync_records_error_when_grpc_fails()
	{
		await using var db = CreateDb();
		var ai = new FakeAiGrpcService { HostProfileError = "Unimplemented" };
		var svc = CreateService(db, ai);

		await svc.RefreshFromWorkerAsync();

		(await db.AiWorkerHostProfiles.CountAsync()).Should().Be(0);
		var meta = await db.AiWorkerHostRefreshMetas.SingleAsync();
		meta.LastRefreshSucceeded.Should().BeFalse();
		meta.LastRefreshError.Should().Be("Unimplemented");
	}

	[Fact]
	public async Task GetOperatorViewAsync_returns_null_profile_when_never_collected()
	{
		await using var db = CreateDb();
		var svc = CreateService(db, new FakeAiGrpcService());

		var view = await svc.GetOperatorViewAsync();

		view.Profile.Should().BeNull();
		view.Reachable.Should().BeFalse();
	}

	private static AiWorkerHostProfileService CreateService(
		ApplicationDbContext db,
		IAiGrpcService ai,
		AiServiceOptions? aiOptions = null,
		OperatorAiOptions? operatorAiOptions = null,
		OperatorAiEmbeddingDimStatus? dimStatus = null)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AiService:GrpcAddress"] = "http://localhost:50051",
			})
			.Build();
		return new AiWorkerHostProfileService(
			db,
			ai,
			config,
			Options.Create(aiOptions ?? new AiServiceOptions()),
			Options.Create(operatorAiOptions ?? new OperatorAiOptions()),
			dimStatus ?? new OperatorAiEmbeddingDimStatus(),
			NullLogger<AiWorkerHostProfileService>.Instance);
	}

	[Fact]
	public async Task GetOperatorViewAsync_folds_config_from_options_and_dim_status()
	{
		await using var db = CreateDb();
		var dim = new OperatorAiEmbeddingDimStatus();
		dim.Record(ok: true, actual: 768);
		var svc = CreateService(
			db,
			new FakeAiGrpcService(),
			aiOptions: new AiServiceOptions
			{
				HelperModel = "qwen2.5:3b-instruct-q4_K_M",
				EmbeddingModel = "nomic-embed-text",
				EmbeddingDim = 768,
			},
			operatorAiOptions: new OperatorAiOptions { HelperForDecisions = true },
			dimStatus: dim);

		var view = await svc.GetOperatorViewAsync();

		view.Config.Should().NotBeNull();
		view.Config!.HelperModel.Should().Be("qwen2.5:3b-instruct-q4_K_M");
		view.Config.HelperEnabled.Should().BeTrue();
		view.Config.EmbeddingModel.Should().Be("nomic-embed-text");
		view.Config.EmbeddingDim.Should().Be(768);
		view.Config.EmbeddingDimOk.Should().BeTrue();
		view.Config.EmbeddingDimActual.Should().Be(768);
	}

	[Fact]
	public async Task GetOperatorViewAsync_config_marks_helper_disabled_when_no_helper_model()
	{
		await using var db = CreateDb();
		var svc = CreateService(
			db,
			new FakeAiGrpcService(),
			aiOptions: new AiServiceOptions { HelperModel = null },
			operatorAiOptions: new OperatorAiOptions { HelperForDecisions = true });

		var view = await svc.GetOperatorViewAsync();

		view.Config.Should().NotBeNull();
		view.Config!.HelperModel.Should().BeNull();
		view.Config.HelperEnabled.Should().BeFalse();
		view.Config.EmbeddingDimOk.Should().BeNull(); // probe never ran
	}

	private static string SampleProfileJson(string workerId, string hostname = "DESKTOP-TEST") =>
		$$"""
        {
          "schemaVersion": 1,
          "workerInstanceId": "sha256:{{workerId}}",
          "collectedAtUtc": "2026-05-21T12:00:00Z",
          "scope": "host",
          "hostname": "{{hostname}}",
          "os": { "displayName": "Windows 11 Pro" },
          "cpu": { "logicalCores": 8 },
          "gpu": { "devices": [{ "name": "NVIDIA RTX", "vramBytes": 8589934592 }] },
          "memory": { "ramTotalBytes": 17179869184 }
        }
        """;

}
