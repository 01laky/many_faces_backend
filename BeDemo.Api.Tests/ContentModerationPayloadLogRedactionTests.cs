using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Tests.Testing;

namespace BeDemo.Api.Tests;

/// <summary>
/// Security hardening v2 <b>PI-7</b>: invalid Redis AI review payloads must not leak creator text into logs.
/// </summary>
/// <remarks>SHV2 PI-10: included in CI via <see cref="ContentModerationCiGate.XunitFilterExpression"/>.</remarks>
[Trait(ContentModerationCiGate.XunitTraitName, ContentModerationCiGate.XunitTraitCategory)]
public class ContentModerationPayloadLogRedactionTests
{
	private const string SecretMarker = "PI7_DO_NOT_LOG_THIS_USER_CONTENT_9f3c2a";

	[Fact]
	public void FormatInvalidAiReviewPayloadForLog_NeverIncludesRawSecretPropertyValues()
	{
		var payload = $$"""
            {
              "contentType": "Blog",
              "contentId": 42,
              "title": "{{SecretMarker}}",
              "body": "more {{SecretMarker}}"
            }
            """;

		var diagnostic = ContentModerationHelpers.FormatInvalidAiReviewPayloadForLog(payload);

		diagnostic.Should().NotContain(SecretMarker);
		diagnostic.Should().Contain("contentType=Blog");
		diagnostic.Should().Contain("contentId=42");
		diagnostic.Should().Contain("extraProperties=");
		diagnostic.Should().Contain("sha256Prefix=");
	}

	[Fact]
	public void FormatInvalidAiReviewPayloadForLog_NonJson_ReturnsLengthAndHashOnly()
	{
		var payload = $"not-json {{{SecretMarker}}}";

		var diagnostic = ContentModerationHelpers.FormatInvalidAiReviewPayloadForLog(payload);

		diagnostic.Should().Be($"invalid_json length={payload.Length} sha256Prefix={ContentModerationHelpers.ComputePayloadSha256Prefix(payload)}");
		diagnostic.Should().NotContain(SecretMarker);
	}

	[Fact]
	public void FormatInvalidAiReviewPayloadForLog_Empty_ReturnsEmptyPayloadToken()
	{
		ContentModerationHelpers.FormatInvalidAiReviewPayloadForLog(null).Should().Be("empty_payload");
		ContentModerationHelpers.FormatInvalidAiReviewPayloadForLog("").Should().Be("empty_payload");
	}

	[Fact]
	public void FormatInvalidAiReviewPayloadForLog_MissingRequiredFields_ReportsUnknownIds()
	{
		var diagnostic = ContentModerationHelpers.FormatInvalidAiReviewPayloadForLog(
			"""{"contentType":"Album","contentId":1}""");

		diagnostic.Should().Contain("moderationVersion=?");
		diagnostic.Should().Contain("contentType=Album");
		diagnostic.Should().Contain("contentId=1");
	}

	[Fact]
	public async Task ProcessQueuedReviewAsync_InvalidPayload_DoesNotLogRawJson()
	{
		await using var context = CreateContext();
		var logger = new CollectingLogger<ContentAiReviewService>();
		var service = new ContentAiReviewService(
			context,
			new NoOpAiGrpcService(),
			new NoOpRedisJobQueue(),
			logger,
			new NullContentModerationNotifier(),
			Options.Create(new ContentModerationSecurityOptions()),
			new StubOperatorAiSystemSettingsProvider());

		var hostilePayload = $$"""
            {
              "contentType": "Blog",
              "contentId": 1,
              "moderationVersion": "not-an-int",
              "title": "{{SecretMarker}}",
              "media_url": "https://evil.example/ignore?{{SecretMarker}}"
            }
            """;

		await service.ProcessQueuedReviewAsync(hostilePayload);

		logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
		var message = logger.Entries[0].Message;
		message.Should().Contain("Dropping invalid AI review payload");
		message.Should().NotContain(SecretMarker);
		message.Should().NotContain(hostilePayload);
		message.Should().Contain("sha256Prefix=");
		message.Should().Contain("extraProperties=");
	}

	[Fact]
	public async Task ProcessQueuedReviewAsync_ValidPayload_DoesNotEmitInvalidPayloadWarning()
	{
		await using var context = CreateContext();
		var logger = new CollectingLogger<ContentAiReviewService>();
		var face = new Face { Index = $"face-{Guid.NewGuid():N}", Title = "Log Face" };
		var user = new ApplicationUser
		{
			Id = Guid.NewGuid().ToString("N"),
			UserName = "log-user@example.com",
			Email = "log-user@example.com",
			UserRoleId = 1,
		};
		context.Faces.Add(face);
		context.Users.Add(user);
		var blog = new Blog
		{
			CreatorId = user.Id,
			FaceId = face.Id,
			Title = SecretMarker,
			Content = $"<p>{SecretMarker}</p>",
			ApprovalStatus = ContentApprovalStatus.PendingApproval,
			AiReviewStatus = AiReviewStatus.Queued,
			ModerationVersion = 1,
		};
		context.Blogs.Add(blog);
		await context.SaveChangesAsync();

		var service = new ContentAiReviewService(
			context,
			new NoOpAiGrpcService(),
			new NoOpRedisJobQueue(),
			logger,
			new NullContentModerationNotifier(),
			Options.Create(new ContentModerationSecurityOptions { InstructionHeuristicEnabled = false }),
			new StubOperatorAiSystemSettingsProvider());

		var validPayload = ContentModerationHelpers.BuildAiReviewPayload(
			ModeratedContentType.Blog,
			blog.Id,
			1);

		await service.ProcessQueuedReviewAsync(validPayload);

		logger.Entries.Should().NotContain(e => e.Message.Contains("Dropping invalid AI review payload", StringComparison.Ordinal));
	}

	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"payload-log-redaction-{Guid.NewGuid():N}")
			.Options;
		var context = new ApplicationDbContext(options);
		context.Database.EnsureCreated();
		return context;
	}

	private sealed class NullContentModerationNotifier : IContentModerationNotifier
	{
		public void NotifyCreator(string creatorId, string title, string message, string type = "content_moderation") { }

		public Task NotifySuperAdminsAsync(
			string title,
			string message,
			string type = "moderation_ops",
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;
	}

	private sealed class NoOpRedisJobQueue : IRedisJobQueue
	{
		public Task EnqueueAsync(string jobType, string payloadJson, CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public Task ScheduleAsync(
			string jobType,
			string payloadJson,
			DateTime runAtUtc,
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;
	}

	private sealed class NoOpAiGrpcService : IAiGrpcService
	{
		public Task<AiEmbedTextResult> EmbedTextAsync(string text, string? model = null, CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiEmbedTextResult(null, null, "test fake"));

		public Task<string> GenerateAsync(
			string prompt,
			int maxNewTokens = 50,
			string? statsContextJson = null,
			string? responseLocale = null,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(string.Empty);

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
			Task.FromResult(new AiContentReviewResult(
				new AiReviewRecommendation(
					AiReviewDecision.Approve,
					0.9,
					AiReviewRiskLevel.Low,
					Array.Empty<string>(),
					"ok",
					"msg",
					"m",
					"t"),
				null));

		public Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiModelStatus(true, false, false, "test-model"));

		public Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(new AiHostProfileFetchResult(null, null));
	}
}
