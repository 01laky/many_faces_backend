using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
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

	private static AiWorkerHostProfileService CreateService(ApplicationDbContext db, IAiGrpcService ai)
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
			Options.Create(new AiServiceOptions()),
			NullLogger<AiWorkerHostProfileService>.Instance);
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

	private sealed class FakeAiGrpcService : IAiGrpcService
	{
		public Task<AiEmbedTextResult> EmbedTextAsync(string text, string? model = null, CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiEmbedTextResult(null, null, "test fake"));

		public Task<AiGenerateReportResult> GenerateReportAsync(string reportType, string inputJson, int maxNewTokens, CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiGenerateReportResult(null, null, null, "test fake"));

		public string? HostProfileJson { get; set; }
		public string? HostProfileError { get; set; }

		public Task<string> GenerateAsync(
			string prompt,
			int maxNewTokens = 50,
			string? statsContextJson = null,
			string? responseLocale = null,
			double? temperature = null,
			IReadOnlyList<string>? stopSequences = null,
			string? model = null,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(string.Empty);

		public async System.Collections.Generic.IAsyncEnumerable<AiGenerateDelta> GenerateStreamAsync(

			string prompt,

			int maxNewTokens = 50,

			string? statsContextJson = null,

			string? responseLocale = null,

			double? temperature = null,

			IReadOnlyList<string>? stopSequences = null,

			string? model = null,

			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)

		{

			var text = await GenerateAsync(prompt, maxNewTokens, statsContextJson, responseLocale, temperature, stopSequences, model, cancellationToken);

			yield return new AiGenerateDelta(text, true, "stop", null, null);

		}


		public Task<string> OperatorStatsChatAsync(
			string userMessage,
			string historyText,
			bool fetchLivePublicSnapshot,
			string publicStatsAbsoluteUrl,
			int maxNewTokens = 150,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(string.Empty);

		public Task<AiContentReviewResult> ReviewContentAsync(
			AiContentReviewRequest request,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiContentReviewResult(null, null));

		public Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiModelStatus(false, false, true, null));

		public Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiHostProfileFetchResult(HostProfileJson, HostProfileError));
	}
}
