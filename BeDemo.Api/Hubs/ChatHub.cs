/*
 * ChatHub.cs - SignalR Hub for real-time chat communication
 *
 * Operator AI: shared support inbox via SendToAiWithOperatorStats(conversationId, message)
 * with DB persistence and operator_ai_operators group broadcasts.
 *
 * RAG retrieval refactor v1 (operator-ai-rag-retrieval-refactor-v1):
 *   The operator chat is now ALWAYS data-grounded (retrieve → map → stitch). The off/inline stats modes and the
 *   responseLocale arg are removed (D10/D11). One unified orchestrator: IOperatorAiRetriever does SELECTION
 *   (RAG-first, planner fallback when embed/ES is down or the index isn't ready, zero-hit escalation), and the
 *   retained per-bundle map + stitch turns the fresh-loaded top-K bundles into one English reply.
 */

using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BeDemo.Api.Services.OperatorAi.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
	/// <summary>
	/// Fixed English refusal (§6.1) returned after RAG + all zero-hit escalation attempts find nothing usable.
	/// Lives here as a const (no locale variants, D10); the parent may relocate it to the operator-AI resource set.
	/// </summary>
	private const string ZeroHitRefusal =
		"Many Faces AI only answers questions about data within the application. " +
		"I couldn't find anything in the platform data that matches your question.";

	/// <summary>The chat is always grounded + English now (D10/D11); persistence pins these to keep the DB contract.</summary>
	private const string PersistedResponseLocale = "en";
	private const string PersistedStatsMode = "live";

	private readonly ILogger<ChatHub> _logger;
	private readonly IAiGrpcService _aiGrpcService;
	private readonly IChatHubAiRateLimiter _aiRateLimiter;
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IConfiguration _configuration;
	private readonly IOperatorAiConversationService _operatorAi;
	private readonly IOperatorAiSkillRouter _skillRouter;
	private readonly IOperatorAiSystemSettingsProvider _systemSettings;
	private readonly IOperatorAiActiveGenerationGuard _activeGenerationGuard;
	private readonly IOperatorAiAnswerCache _answerCache;
	private readonly IOperatorAiFollowUpResolver _followUpResolver;
	private readonly OperatorAiOptions _operatorAiOptions;

	public ChatHub(
		ILogger<ChatHub> logger,
		IAiGrpcService aiGrpcService,
		IChatHubAiRateLimiter aiRateLimiter,
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IConfiguration configuration,
		IOperatorAiConversationService operatorAi,
		IOperatorAiSkillRouter skillRouter,
		IOperatorAiSystemSettingsProvider systemSettings,
		IOperatorAiActiveGenerationGuard activeGenerationGuard,
		IOperatorAiAnswerCache answerCache,
		IOperatorAiFollowUpResolver followUpResolver,
		IOptions<OperatorAiOptions> operatorAiOptions)
	{
		_logger = logger;
		_aiGrpcService = aiGrpcService;
		_aiRateLimiter = aiRateLimiter;
		_context = context;
		_faceScope = faceScope;
		_configuration = configuration;
		_operatorAi = operatorAi;
		_skillRouter = skillRouter;
		_systemSettings = systemSettings;
		_activeGenerationGuard = activeGenerationGuard;
		_answerCache = answerCache;
		_followUpResolver = followUpResolver;
		_operatorAiOptions = operatorAiOptions.Value;
	}

	private static string FaceChatBroadcastGroup(int faceId) => $"hubchat_face_{faceId}";

	private bool CanManageAllFaces() =>
		Context.User != null && PlatformAccessRules.CanManageAllFaces(_faceScope, Context.User);

	public override async Task OnConnectedAsync()
	{
		if (!_faceScope.IsAvailable)
		{
			_logger.LogWarning("ChatHub connection rejected: no face scope (use /{{face}}/hubs/chat)");
			Context.Abort();
			return;
		}

		var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

		_logger.LogInformation("User {UserId} connected to SignalR hub", userId);

		if (!string.IsNullOrEmpty(userId))
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
			await Groups.AddToGroupAsync(Context.ConnectionId, FaceChatBroadcastGroup(_faceScope.FaceId));
		}

		if (CanManageAllFaces())
			await Groups.AddToGroupAsync(Context.ConnectionId, OperatorAiHubGroups.Operators);

		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

		if (exception != null)
			_logger.LogWarning(exception, "User {UserId} disconnected from SignalR hub with error", userId);
		else
			_logger.LogInformation("User {UserId} disconnected from SignalR hub", userId);

		if (!string.IsNullOrEmpty(userId))
		{
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
			if (_faceScope.IsAvailable)
				await Groups.RemoveFromGroupAsync(Context.ConnectionId, FaceChatBroadcastGroup(_faceScope.FaceId));
		}

		if (CanManageAllFaces())
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, OperatorAiHubGroups.Operators);

		await base.OnDisconnectedAsync(exception);
	}

	public async Task SendMessage(string user, string message)
	{
		if (!_faceScope.IsAvailable)
			return;

		var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;

		_logger.LogInformation(
			"User {UserId} sent hub message ({MessageMeta})",
			userId,
			PiiLogRedaction.FormatChatMessageForLog(message));

		await Clients.Group(FaceChatBroadcastGroup(_faceScope.FaceId)).SendAsync("ReceiveMessage", user, message);
	}

	public async Task SendPrivateMessage(string targetUserId, string message)
	{
		if (!_faceScope.IsAvailable)
			return;

		var senderId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(targetUserId))
			return;

		if (!CanManageAllFaces() &&
			!await TenantSocialScopeRules.BothUsersParticipateInFaceAsync(_context, _faceScope.FaceId, senderId, targetUserId))
			return;

		_logger.LogInformation("User {UserId} sent private message to {TargetUserId}", senderId, targetUserId);

		await Clients.User(targetUserId).SendAsync("ReceivePrivateMessage", senderId, message);
	}

	public async Task SendToAi(string message, ChatHistoryEntry[]? history = null)
	{
		var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
		_logger.LogInformation(
			"User {UserId} sent message to AI ({MessageMeta})",
			userId,
			PiiLogRedaction.FormatChatMessageForLog(message));

		if (!_aiRateLimiter.TryAllow(userId))
		{
			await Clients.Caller.SendAsync(
				"ReceiveAiMessage",
				message ?? string.Empty,
				"You are sending too many AI requests. Please wait a moment and try again.");
			return;
		}

		if (!await _systemSettings.IsAiEnabledAsync(Context.ConnectionAborted))
		{
			await Clients.Caller.SendAsync(
				"ReceiveAiMessage",
				message ?? string.Empty,
				"AI support is currently disabled for this system.");
			return;
		}

		string aiResponse;
		try
		{
			var prompt = BuildPromptWithHistory(message ?? string.Empty, history);
			aiResponse = await _aiGrpcService.GenerateAsync(prompt, maxNewTokens: 150);

			if (string.IsNullOrWhiteSpace(aiResponse))
				aiResponse = "...";
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "SendToAi failed for user {UserId}", userId);
			aiResponse = "Error: AI service is currently unavailable. Please try again later.";
		}

		await Clients.Caller.SendAsync("ReceiveAiMessage", message ?? string.Empty, aiResponse);
	}

	/// <summary>
	/// Operator shared inbox — the single, always-data-grounded operator AI turn (RAG refactor v1, §6/§17).
	///
	/// <para>Removed (D10/D11):</para>
	/// the <c>statsMode</c> (off/inline) and <c>responseLocale</c> parameters. The chat is always
	/// <c>retrieve → map → stitch</c> and always answers in English; there is no free-chat / dashboard-dump path.
	///
	/// <para>Pipeline:</para>
	/// guards (global-enable, ACL, message length, conversation, rate-limit, model-ready) — unchanged →
	/// <see cref="IOperatorAiRetriever"/> SELECTION (RAG-first; planner fallback when embed/ES is down or the index
	/// isn't ready; up to <c>ZeroHitRetryAttempts</c> escalation; else the fixed English refusal) →
	/// fresh-load ONLY the selected K bundles + retained per-bundle map + stitch (one English reply) →
	/// persist ONE assistant message + SignalR broadcast (unchanged).
	/// </summary>
	/// <param name="conversationId">Target shared-inbox thread.</param>
	/// <param name="message">Operator question.</param>
	/// <remarks>
	/// The wire contract is exactly <c>(conversationId, message)</c>. There is no optional per-turn
	/// parallelism argument: ASP.NET Core SignalR does NOT support optional / defaulted hub-method
	/// parameters — a 2-argument client invoke against a 3-parameter method fails argument binding on
	/// the server ("Failed to invoke 'SendToAiWithOperatorStats' due to an error on the server") BEFORE
	/// the method body runs, which silently broke every operator AI chat turn. The bundle-parallelism
	/// cap is read from <see cref="OperatorAiOptions.MaxParallelBundleAiCalls"/> instead (no client ever
	/// sent an override).
	/// </remarks>
	public async Task SendToAiWithOperatorStats(
		int conversationId,
		string message)
	{
		var userId = Context.UserIdentifier ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		_logger.LogInformation(
			"User {UserId} sent operator stats AI message (conversation {ConversationId}, {MessageMeta})",
			userId,
			conversationId,
			PiiLogRedaction.FormatChatMessageForLog(message));

		var trimmed = (message ?? string.Empty).Trim();

		// ── Guards (unchanged): global enable, SUPER_ADMIN ACL, length, conversation, rate-limit, model-ready ──
		if (!await _systemSettings.IsAiEnabledAsync(Context.ConnectionAborted))
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.AiDisabled);
			return;
		}

		if (!CanManageAllFaces())
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.NotOperator);
			return;
		}

		if (string.IsNullOrEmpty(userId))
			return;

		if (trimmed.Length == 0)
			return;

		if (trimmed.Length > _operatorAiOptions.MaxMessageLength)
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.MessageTooLong);
			return;
		}

		var conversation = await _operatorAi.GetConversationAsync(conversationId, Context.ConnectionAborted);
		if (conversation == null)
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.ConversationNotFound);
			return;
		}

		if (!_aiRateLimiter.TryAllow(userId))
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.RateLimited);
			return;
		}

		var modelStatus = await _aiGrpcService.GetModelStatusAsync(Context.ConnectionAborted);
		if (!modelStatus.Ready)
		{
			var code = modelStatus.Unavailable
				? OperatorAiHubErrorCodes.AiUnavailable
				: OperatorAiHubErrorCodes.ModelLoading;
			await SendOperatorAiEphemeralAsync(trimmed, code);
			return;
		}

		var operatorEmail = await ResolveOperatorEmailAsync(userId, Context.ConnectionAborted);
		if (string.IsNullOrWhiteSpace(operatorEmail))
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.NotOperator);
			return;
		}

		// Per-turn bundle parallelism is the configured cap (≥ 1). This was previously an optional hub
		// argument, but SignalR cannot carry optional parameters (see the method remarks), and no client
		// ever supplied an override, so the configured ceiling is used directly.
		var effectiveParallel = Math.Max(1, _operatorAiOptions.MaxParallelBundleAiCalls);

		// 7B-perf O17 — single-active-generation guard: the local GPU is serial, so reject a second concurrent turn
		// for this conversation (rather than thrashing both). Released in the finally below.
		if (_operatorAiOptions.SingleActiveGenerationGuardEnabled && !_activeGenerationGuard.TryBegin(conversationId))
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.GenerationInProgress);
			return;
		}

		try
		{
			string aiResponse;
			string routedSkillId;
			string resolvedMessage;
			// Operator-AI message duration: measure the whole turn the operator waited for — routing + retrieval +
			// generation. Stopped once the answer is finalized (before the response guard / persistence), so the
			// stored value reflects compute time, not the DB write + SignalR broadcast.
			var turnStopwatch = Stopwatch.StartNew();
			try
			{
				// -- CONVERSATIONAL CONTEXT (A1): resolve an anaphoric follow-up before routing --
				// The deterministic resolver carries the last named entity (e.g. "All active?" → "reels All active?")
				// so the bare anaphor routes + retrieves correctly. Routing / RAG / broad / answer-cache all see the
				// RESOLVED query; the ORIGINAL `trimmed` is what we persist and display (below). When no carry fires
				// the resolver returns `trimmed` unchanged, so this is a no-op on a self-contained turn.
				resolvedMessage = _followUpResolver.Resolve(trimmed, conversationId);

				// -- SKILL ROUTING (operator-ai-skills v1): route to one skill, then run it --
				var route = await _skillRouter.RouteAsync(resolvedMessage, Context.ConnectionAborted);
				routedSkillId = route.Skill.Id;
				if (_operatorAiOptions.RetrievalTraceEnabled)
				{
					_logger.LogDebug(
						"Operator AI skill route: skill={SkillId} score={Score:0.###} fallback={Fallback}",
						route.Skill.Id,
						route.Score,
						route.Fallback);
				}

				var skillRequest = new OperatorAiSkillRequest(
					resolvedMessage, conversationId, Array.Empty<ChatHistoryEntry>(), effectiveParallel, route.Score);

				// 7B-perf O18 — exact-repeat answer cache: an identical (skill, resolved message) within TTL skips the
				// turn. Keyed on the RESOLVED query so two phrasings that resolve to the same question share an entry
				// and a bare anaphor ("all active?") is never cached on its own (§6).
				if (_answerCache.TryGet(routedSkillId, resolvedMessage, out var cachedAnswer))
				{
					aiResponse = cachedAnswer;
				}
				// 7B-perf O4 — stream the terminal generation when the routed skill supports it.
				else if (_operatorAiOptions.StreamingEnabled && route.Skill is IOperatorAiStreamingSkill streamingSkill)
				{
					aiResponse = await RunStreamingSkillAsync(streamingSkill, skillRequest, conversationId);
				}
				else
				{
					var skillResult = await route.Skill.RunAsync(skillRequest, Context.ConnectionAborted);
					aiResponse = skillResult.AnswerMarkdown;
				}

				if (string.IsNullOrWhiteSpace(aiResponse))
					aiResponse = "...";
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SendToAiWithOperatorStats failed for user {UserId}", userId);
				await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.AiGenerationFailed);
				return;
			}

			// Answer finalized — freeze the duration before the guard / persistence.
			turnStopwatch.Stop();

			if (OperatorAiResponseGuard.ShouldNotPersist(aiResponse))
			{
				// Optional stale-answer fallback (off by default) — when the AI is down but a previous successful
				// answer for the SAME (skill, question) is still retained, serve it with a "may be stale" note as a
				// caller-only (non-persisted) reply instead of the bare ephemeral error. Mirrors the answer-cache key.
				if (_operatorAiOptions.StaleAnswerFallbackEnabled
					&& _answerCache.TryGetStale(routedSkillId, resolvedMessage, out var staleAnswer))
				{
					var staleWithNote = staleAnswer
						+ "\n\n_(This answer may be stale — Many Faces AI was unavailable, so the last successful result is shown.)_";
					_logger.LogWarning(
						"Operator AI degraded turn for conversation {ConversationId}; serving a stale cached answer (not persisting).",
						conversationId);
					await Clients.Caller.SendAsync("ReceiveAiMessage", trimmed, staleWithNote, OperatorAiHubErrorCodes.AiStale);
					return;
				}

				// operator-ai degraded-handling D11/D12 — pick the honest, localized ephemeral code by failure kind
				// instead of one generic "guard rejected": model still loading → ModelLoading; an infrastructure /
				// AI-unavailable failure (incl. the all-failed stats sentinel) → AiUnavailable; otherwise the generic
				// guard. The user-facing copy comes from the resx-localized code (en/sk/cs/de/fr/it).
				var degradedCode = OperatorAiResponseGuard.IsTransientStatusMessage(aiResponse)
					? OperatorAiHubErrorCodes.ModelLoading
					: OperatorAiResponseGuard.IsInfrastructureFailure(aiResponse)
						? OperatorAiHubErrorCodes.AiUnavailable
						: OperatorAiHubErrorCodes.AiGuardRejected;
				_logger.LogWarning(
					"Operator AI degraded turn for conversation {ConversationId} (code {Code}); not persisting.",
					conversationId,
					degradedCode);
				// operator-ai degraded-handling D19 — count the degraded turn (PII-free, tagged by failure code) so
				// monitoring catches a recurring degradation proactively instead of at the next demo.
				OperatorAiMetrics.RecordDegradedTurn(degradedCode);
				await SendOperatorAiEphemeralAsync(trimmed, degradedCode);
				return;
			}

			aiResponse = OperatorAiResponseGuard.ToUserFacingMessage(aiResponse);

			// 7B-perf O18 — remember this answer for identical repeats (no-op when the cache is disabled). Keyed on the
			// RESOLVED query (A1, §6) to mirror the lookup above — never on the bare anaphor.
			_answerCache.Set(routedSkillId, resolvedMessage, aiResponse);

			// Persist ONE assistant message + the user turn. The chat is always grounded/English now, so the locale
			// and stats-mode columns are pinned to "en" / "live" (no off/inline, D11) — preserving the persistence
			// contract without a schema change.
			var (userDto, assistantDto) = await _operatorAi.AppendExchangeAsync(
				conversationId,
				userId,
				operatorEmail,
				PersistedResponseLocale,
				trimmed,
				aiResponse,
				PersistedStatsMode,
				turnStopwatch.ElapsedMilliseconds,
				Context.ConnectionAborted);

			var updatedConversation = await _operatorAi.GetConversationAsync(conversationId, Context.ConnectionAborted)
				?? conversation;

			var appended = new OperatorAiMessageAppendedEventDto
			{
				ConversationId = conversationId,
				UserMessage = userDto,
				AssistantMessage = assistantDto,
				Conversation = updatedConversation,
			};

			await Clients.Group(OperatorAiHubGroups.Operators).SendAsync("OperatorAiMessageAppended", appended);
			await Clients.Group(OperatorAiHubGroups.Operators).SendAsync("OperatorAiConversationListChanged", updatedConversation);
		}
		finally
		{
			// O17 — always release the conversation, even on early return / exception.
			_activeGenerationGuard.End(conversationId);
		}
	}

	/// <summary>
	/// 7B-perf O4 — run a streaming skill, forwarding each token delta to the operator group as an
	/// <c>OperatorAiMessageDelta</c> event, and return the full accumulated answer (which the caller persists once).
	/// </summary>
	private async Task<string> RunStreamingSkillAsync(
		IOperatorAiStreamingSkill skill,
		OperatorAiSkillRequest request,
		int conversationId)
	{
		var finalAnswer = string.Empty;
		await foreach (var chunk in skill
			.RunStreamingAsync(request, Context.ConnectionAborted)
			.WithCancellation(Context.ConnectionAborted))
		{
			if (!string.IsNullOrEmpty(chunk.Delta))
			{
				await Clients.Group(OperatorAiHubGroups.Operators).SendAsync(
					"OperatorAiMessageDelta",
					new OperatorAiMessageDeltaEventDto { ConversationId = conversationId, Delta = chunk.Delta! });
			}
			if (chunk.IsFinal)
				finalAnswer = chunk.FinalAnswer ?? finalAnswer;
		}
		return finalAnswer;
	}

	private async Task SendOperatorAiEphemeralAsync(string userText, string hubErrorCode)
	{
		await Clients.Caller.SendAsync("ReceiveAiMessage", userText ?? string.Empty, string.Empty, hubErrorCode);
	}

	private async Task<string?> ResolveOperatorEmailAsync(string userId, CancellationToken cancellationToken)
	{
		var email = await _context.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => u.Email)
			.FirstOrDefaultAsync(cancellationToken);

		if (!string.IsNullOrWhiteSpace(email))
			return email.Trim();

		return Context.User?.FindFirstValue(ClaimTypes.Email)?.Trim();
	}

	private static string BuildPromptWithHistory(string message, ChatHistoryEntry[]? history)
	{
		var sb = new StringBuilder();
		sb.Append("[Server clock: ")
			.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
			.AppendLine(" UTC]");
		if (history != null && history.Length > 0)
		{
			foreach (var entry in history)
			{
				var u = entry.UserMessage ?? string.Empty;
				var a = entry.AiResponse ?? string.Empty;
				sb.Append("User: ").AppendLine(u);
				sb.Append("AI: ").AppendLine(a);
			}
		}

		sb.Append("User: ").AppendLine(message);
		sb.Append("AI:");
		return sb.ToString();
	}
}
