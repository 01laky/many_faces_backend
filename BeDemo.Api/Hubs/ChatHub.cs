/*
 * ChatHub.cs - SignalR Hub for real-time chat communication
 *
 * Operator AI: shared support inbox via SendToAiWithOperatorStats(conversationId, message, statsMode, responseLocale, maxParallelBundleAiCalls?)
 * with DB persistence and operator_ai_operators group broadcasts.
 */

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
	private static readonly JsonSerializerOptions PublicStatsJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private readonly ILogger<ChatHub> _logger;
	private readonly IAiGrpcService _aiGrpcService;
	private readonly IChatHubAiRateLimiter _aiRateLimiter;
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IPlatformStatsQueryService _platformStats;
	private readonly IConfiguration _configuration;
	private readonly IOperatorAiConversationService _operatorAi;
	private readonly IOperatorAiLiveStatsOrchestrator _liveStatsOrchestrator;
	private readonly IOperatorAiSystemSettingsProvider _systemSettings;
	private readonly OperatorAiOptions _operatorAiOptions;

	public ChatHub(
		ILogger<ChatHub> logger,
		IAiGrpcService aiGrpcService,
		IChatHubAiRateLimiter aiRateLimiter,
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IPlatformStatsQueryService platformStats,
		IConfiguration configuration,
		IOperatorAiConversationService operatorAi,
		IOperatorAiLiveStatsOrchestrator liveStatsOrchestrator,
		IOperatorAiSystemSettingsProvider systemSettings,
		IOptions<OperatorAiOptions> operatorAiOptions)
	{
		_logger = logger;
		_aiGrpcService = aiGrpcService;
		_aiRateLimiter = aiRateLimiter;
		_context = context;
		_faceScope = faceScope;
		_platformStats = platformStats;
		_configuration = configuration;
		_operatorAi = operatorAi;
		_liveStatsOrchestrator = liveStatsOrchestrator;
		_systemSettings = systemSettings;
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
	/// Operator shared inbox: persists turns to DB, loads history server-side, broadcasts to <see cref="OperatorAiHubGroups.Operators"/>.
	/// </summary>
	public async Task SendToAiWithOperatorStats(
		int conversationId,
		string message,
		string? statsMode,
		string? responseLocale,
		int? maxParallelBundleAiCalls = null)
	{
		var userId = Context.UserIdentifier ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		_logger.LogInformation(
			"User {UserId} sent operator stats AI message (conversation {ConversationId}, mode {Mode}, locale {Locale})",
			userId,
			conversationId,
			statsMode ?? "off",
			responseLocale ?? "(missing)");

		var trimmed = (message ?? string.Empty).Trim();

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

		if (!OperatorAiLocaleValidator.TryNormalize(responseLocale, out var locale))
		{
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.InvalidLocale);
			return;
		}

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

		var mode = (statsMode ?? "off").Trim().ToLowerInvariant();
		if (mode is not ("off" or "inline" or "live"))
			mode = "off";

		var history = await _operatorAi.GetRecentHistoryPairsAsync(
			conversationId,
			_operatorAiOptions.MaxHistoryPairs,
			Context.ConnectionAborted);

		var maxTokens = _operatorAiOptions.MaxNewTokens;
		var effectiveParallel = Math.Clamp(
			maxParallelBundleAiCalls ?? 2,
			1,
			_operatorAiOptions.MaxParallelBundleAiCalls);
		string aiResponse;
		try
		{
			if (mode == "live")
			{
				// Live map-reduce: prefetch cache → planner indices → queued per-bundle AI → stitch.
				aiResponse = await _liveStatsOrchestrator.RunAsync(
					trimmed,
					locale,
					effectiveParallel,
					Context.ConnectionAborted);
			}
			else
			{
				var prompt = BuildPromptWithHistory(trimmed, history.ToArray());
				string? statsJson = null;
				if (mode == "inline" && ShouldAttachStatsContext(trimmed))
					statsJson = await BuildOperatorStatsContextJsonAsync(trimmed, Context.ConnectionAborted);

				aiResponse = await _aiGrpcService.GenerateAsync(
					prompt,
					maxNewTokens: maxTokens,
					statsContextJson: statsJson,
					responseLocale: locale,
					cancellationToken: Context.ConnectionAborted);
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

		if (OperatorAiResponseGuard.ShouldNotPersist(aiResponse))
		{
			_logger.LogWarning(
				"Non-chat AI status returned for conversation {ConversationId}, not persisting.",
				conversationId);
			await SendOperatorAiEphemeralAsync(trimmed, OperatorAiHubErrorCodes.AiGuardRejected);
			return;
		}

		aiResponse = OperatorAiResponseGuard.ToUserFacingMessage(aiResponse);

		var (userDto, assistantDto) = await _operatorAi.AppendExchangeAsync(
			conversationId,
			userId,
			operatorEmail,
			locale,
			trimmed,
			aiResponse,
			mode,
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

	private bool ShouldAttachStatsContext(string userMessage)
	{
		if (!_operatorAiOptions.AttachStatsOnlyForMetricsQuestions)
			return true;
		return OperatorAiStatsIntent.IsMetricsQuestion(userMessage);
	}

	private async Task<string> BuildOperatorStatsContextJsonAsync(
		string userMessage,
		CancellationToken cancellationToken)
	{
		var dashboard = await _platformStats.GetOperatorDashboardSummaryAsync(cancellationToken);
		OperatorAiTimeseriesHintsDto? timeseries = null;
		if (_operatorAiOptions.IncludeTimeseriesInStatsContext
			&& OperatorAiStatsIntent.IsMetricsQuestion(userMessage))
		{
			timeseries = await _platformStats.GetOperatorAiTimeseriesHintsAsync(cancellationToken);
		}

		var payload = new OperatorAiStatsContextDto
		{
			SnapshotUtc = DateTime.UtcNow,
			Dashboard = dashboard,
			TimeseriesLast7Days = timeseries,
		};
		return JsonSerializer.Serialize(payload, PublicStatsJsonOptions);
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

	private static string BuildHistoryPlainText(ChatHistoryEntry[]? history)
	{
		if (history == null || history.Length == 0)
			return string.Empty;

		var sb = new StringBuilder();
		foreach (var entry in history)
		{
			sb.Append("User: ").AppendLine(entry.UserMessage ?? string.Empty);
			sb.Append("AI: ").AppendLine(entry.AiResponse ?? string.Empty);
		}

		return sb.ToString();
	}
}
