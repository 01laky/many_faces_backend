using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.OperatorAi.Skills;
using BeDemo.Api.Tests.TestDoubles;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai degraded-handling D16 — the END-TO-END exit path through <see cref="ChatHub.SendToAiWithOperatorStats"/>.
/// The orchestrator/guard unit tests prove the classification; this proves the HUB wiring the demo bug actually hit:
/// when the routed skill returns an infrastructure / loading sentinel, the hub must (a) NOT persist it (no half-answer
/// stored), and (b) emit ONE honest ephemeral with the correctly-mapped error code (AiUnavailable vs ModelLoading) —
/// never the hybrid "33 users, but the AI is unavailable" answer. A healthy answer is the positive control: it IS
/// persisted and no ephemeral fires. A fake non-streaming skill returns the answer so the test isolates the hub.
/// </summary>
public sealed class OperatorAiDegradedChatHubIntegrationTests
{
	private const int ConversationId = 7;
	private const string UserId = "user-1";

	/// <summary>A non-streaming skill that returns a fixed answer — isolates the hub's persist/ephemeral wiring.</summary>
	private sealed class FixedAnswerSkill(string answer) : IOperatorAiSkill
	{
		public string Id => "stats";
		public string DisplayName => "Platform statistics";
		public string Description => string.Empty;
		public IReadOnlyList<string> SampleRequests => Array.Empty<string>();
		public string RouterHint => string.Empty;
		public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;
		public Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken) =>
			Task.FromResult(new OperatorAiSkillResult(answer));
	}

	private sealed record HubHarness(
		ChatHub Hub,
		Mock<IOperatorAiConversationService> Conversation,
		Func<(string? Method, object?[]? Args)> LastCallerSend);

	private static HubHarness BuildHub(string skillAnswer, string? staleAnswer = null)
	{
		// SignalR caller proxy — capture the single ephemeral SendAsync("ReceiveAiMessage", user, "", code) call.
		string? method = null;
		object?[]? args = null;
		var caller = new Mock<ISingleClientProxy>();
		caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
			.Callback((string m, object?[] a, CancellationToken _) => { method = m; args = a; })
			.Returns(Task.CompletedTask);
		// Group proxy for the success path (OperatorAiMessageAppended / ConversationListChanged broadcasts).
		var groupProxy = new Mock<IClientProxy>();
		groupProxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		var clients = new Mock<IHubCallerClients>();
		clients.Setup(c => c.Caller).Returns(caller.Object);
		clients.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);

		// SUPER_ADMIN principal on an admin face scope ⇒ CanManageAllFaces() passes; email claim feeds operator resolve.
		var identity = new ClaimsIdentity(
			new[]
			{
				new Claim(ClaimTypes.NameIdentifier, UserId),
				new Claim(ClaimTypes.Email, "operator@example.com"),
				new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.SuperAdmin),
			},
			"test", ClaimTypes.Name, ClaimTypes.Role);
		var context = new Mock<HubCallerContext>();
		context.SetupGet(c => c.UserIdentifier).Returns(UserId);
		context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(identity));
		context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

		var faceScope = new Mock<IFaceScopeContext>();
		faceScope.SetupGet(f => f.IsAdminFaceScope).Returns(true);

		var systemSettings = new Mock<IOperatorAiSystemSettingsProvider>();
		systemSettings.Setup(s => s.IsAiEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

		var rateLimiter = new Mock<IChatHubAiRateLimiter>();
		rateLimiter.Setup(r => r.TryAllow(It.IsAny<string>())).Returns(true);

		var conversation = new Mock<IOperatorAiConversationService>();
		conversation.Setup(c => c.GetConversationAsync(ConversationId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiConversationListItemDto { Id = ConversationId, CreatedByUserId = UserId });
		conversation.Setup(c => c.AppendExchangeAsync(
				It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
				It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((new OperatorAiMessageDto(), new OperatorAiMessageDto()));

		var router = new Mock<IOperatorAiSkillRouter>();
		router.Setup(r => r.RouteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OperatorAiSkillRoute(new FixedAnswerSkill(skillAnswer), 1.0, Fallback: false));

		var guard = new Mock<IOperatorAiActiveGenerationGuard>();
		guard.Setup(g => g.TryBegin(It.IsAny<int>())).Returns(true);

		var answerCache = new Mock<IOperatorAiAnswerCache>();
		var cachedNull = string.Empty;
		answerCache.Setup(a => a.TryGet(It.IsAny<string>(), It.IsAny<string>(), out cachedNull)).Returns(false);
		// Optional stale fallback: when a stale answer is provided, the cache returns it from TryGetStale.
		var stale = staleAnswer ?? string.Empty;
		answerCache.Setup(a => a.TryGetStale(It.IsAny<string>(), It.IsAny<string>(), out stale)).Returns(staleAnswer is not null);

		var followUp = new Mock<IOperatorAiFollowUpResolver>();
		followUp.Setup(f => f.Resolve(It.IsAny<string>(), It.IsAny<int>())).Returns((string m, int _) => m);

		// Streaming OFF so the non-streaming FixedAnswerSkill.RunAsync is used (no IOperatorAiStreamingSkill branch).
		var options = Options.Create(new OperatorAiOptions { StreamingEnabled = false, StaleAnswerFallbackEnabled = staleAnswer is not null });

		var hub = new ChatHub(
			NullLogger<ChatHub>.Instance,
			new FakeAiGrpcService(), // GetModelStatusAsync → ready (default), so the top guard passes and we reach the skill
			rateLimiter.Object,
			InMemoryDb.Fresh(),
			faceScope.Object,
			new ConfigurationBuilder().Build(),
			conversation.Object,
			router.Object,
			systemSettings.Object,
			guard.Object,
			answerCache.Object,
			followUp.Object,
			options)
		{
			Context = context.Object,
			Clients = clients.Object,
		};

		return new HubHarness(hub, conversation, () => (method, args));
	}

	private static void AssertNotPersisted(Mock<IOperatorAiConversationService> conversation) =>
		conversation.Verify(c => c.AppendExchangeAsync(
			It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
			It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never());

	[Fact]
	public async Task All_failed_sentinel_emits_AiUnavailable_ephemeral_and_does_not_persist()
	{
		var h = BuildHub(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);

		await h.Hub.SendToAiWithOperatorStats(ConversationId, "stats about everything");

		var (sendMethod, sendArgs) = h.LastCallerSend();
		sendMethod.Should().Be("ReceiveAiMessage", "the honest unavailable message is an ephemeral to the caller, not a stored turn");
		sendArgs.Should().NotBeNull();
		sendArgs![2].Should().Be(OperatorAiHubErrorCodes.AiUnavailable, "an infrastructure sentinel maps to AiUnavailable");
		AssertNotPersisted(h.Conversation);
	}

	[Fact]
	public async Task Loading_sentinel_emits_ModelLoading_ephemeral_and_does_not_persist()
	{
		var h = BuildHub(OperatorAiLiveStatsOrchestrator.ModelLoadingSentinel);

		await h.Hub.SendToAiWithOperatorStats(ConversationId, "stats about everything");

		var (sendMethod, sendArgs) = h.LastCallerSend();
		sendMethod.Should().Be("ReceiveAiMessage");
		sendArgs![2].Should().Be(OperatorAiHubErrorCodes.ModelLoading, "a loading sentinel maps to ModelLoading, not AiUnavailable");
		AssertNotPersisted(h.Conversation);
	}

	[Fact]
	public async Task The_demo_hybrid_answer_is_caught_and_not_persisted()
	{
		// The exact demo-day hybrid: a real count + a model-narrated "AI unavailable" apology. D5 must catch it.
		var hybrid = "The total number of system users in our application is 33. Unfortunately, the AI service is "
			+ "currently unavailable, so I cannot provide additional details or counts from other entities.";
		var h = BuildHub(hybrid);

		await h.Hub.SendToAiWithOperatorStats(ConversationId, "info about everything");

		var (sendMethod, sendArgs) = h.LastCallerSend();
		sendMethod.Should().Be("ReceiveAiMessage", "the hybrid must be rejected, never stored as the assistant turn");
		sendArgs![2].Should().Be(OperatorAiHubErrorCodes.AiUnavailable);
		AssertNotPersisted(h.Conversation);
	}

	[Fact]
	public async Task A_healthy_answer_is_persisted_and_no_ephemeral_fires()
	{
		// Positive control: a normal grounded answer is stored (AppendExchange called) and no ephemeral is sent.
		var h = BuildHub("There are 33 users and 12 active faces on the platform.");

		await h.Hub.SendToAiWithOperatorStats(ConversationId, "how many users and faces");

		h.LastCallerSend().Method.Should().BeNull("a healthy turn does not send an ephemeral error to the caller");
		h.Conversation.Verify(c => c.AppendExchangeAsync(
			ConversationId, UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
			It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once());
	}

	[Fact]
	public async Task Optional_stale_fallback_serves_the_last_answer_with_a_note_and_does_not_persist()
	{
		// With the stale fallback enabled and a retained answer, a degraded (AI-down) turn serves the last successful
		// answer + a "may be stale" note as a caller-only reply (code ai_stale), instead of the bare unavailable
		// ephemeral — and still does not persist it.
		var h = BuildHub(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel, staleAnswer: "There are 33 users on the platform.");

		await h.Hub.SendToAiWithOperatorStats(ConversationId, "how many users");

		var (sendMethod, sendArgs) = h.LastCallerSend();
		sendMethod.Should().Be("ReceiveAiMessage");
		((string?)sendArgs![1]).Should().Contain("There are 33 users on the platform.", "the retained answer is served");
		((string?)sendArgs![1]).Should().Contain("may be stale", "with an honest staleness note");
		sendArgs[2].Should().Be(OperatorAiHubErrorCodes.AiStale);
		AssertNotPersisted(h.Conversation);
	}
}
