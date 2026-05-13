/*
 * ChatHub.cs - SignalR Hub for real-time chat communication
 * 
 * This hub provides WebSocket endpoint for real-time communication.
 * All methods require authentication ([Authorize] attribute).
 * 
 * Endpoint (must run after <c>RoutingMiddleware</c>): <c>wss://host/{face-kebab}/hubs/chat?access_token=&lt;JWT&gt;</c> — same face-prefix rule as REST (ACL A11).
 * 
 * Methods:
 * - SendMessage: Sends message to all connected clients
 * - SendPrivateMessage: Sends private message to specific user
 * - SendToAi: Sends user message to AI service via gRPC, returns AI response to caller (ReceiveAiMessage)
 * - SendToAiWithOperatorStats: Operator-only; optional public aggregate stats (off / inline / live) then same ReceiveAiMessage callback
 * 
 * Events:
 * - OnConnectedAsync: Invoked when client connects
 * - OnDisconnectedAsync: Invoked when client disconnects
 * 
 * Callbacks (client can listen):
 * - ReceiveMessage: Receives message from all clients
 * - ReceivePrivateMessage: Receives private message
 * - ReceiveAiMessage: Receives AI response (userMessage, aiResponse) after SendToAi
 */

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Hubs;

/// <summary>
/// SignalR Hub for chat communication
/// [Authorize] ensures that only authenticated users can access the hub
/// </summary>
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

    public ChatHub(
        ILogger<ChatHub> logger,
        IAiGrpcService aiGrpcService,
        IChatHubAiRateLimiter aiRateLimiter,
        ApplicationDbContext context,
        IFaceScopeContext faceScope,
        IPlatformStatsQueryService platformStats,
        IConfiguration configuration)
    {
        _logger = logger;
        _aiGrpcService = aiGrpcService;
        _aiRateLimiter = aiRateLimiter;
        _context = context;
        _faceScope = faceScope;
        _platformStats = platformStats;
        _configuration = configuration;
    }

    private static string FaceChatBroadcastGroup(int faceId) => $"hubchat_face_{faceId}";

    private bool CanManageAllFaces() =>
        Context.User != null && PlatformAccessRules.CanManageAllFaces(_faceScope, Context.User);

    /// <summary>
    /// Invoked automatically when client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (!_faceScope.IsAvailable)
        {
            _logger.LogWarning("ChatHub connection rejected: no face scope (use /{{face}}/hubs/chat)");
            Context.Abort();
            return;
        }

        // Gets User ID from JWT token
        // Context.UserIdentifier contains value from ClaimTypes.NameIdentifier claim in JWT token
        // If UserIdentifier is not available, tries to get it directly from claims
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation("User {UserId} connected to SignalR hub", userId);

        // Adds user to group "user_{userId}"
        // Groups allow sending messages to specific users or groups of users
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, FaceChatBroadcastGroup(_faceScope.FaceId));
        }

        // Calls base implementation
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Invoked automatically when client disconnects from the hub
    /// </summary>
    /// <param name="exception">Exception if disconnection was caused by error, otherwise null</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Gets User ID
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Logs disconnection (with error information if exists)
        if (exception != null)
        {
            _logger.LogWarning(exception, "User {UserId} disconnected from SignalR hub with error", userId);
        }
        else
        {
            _logger.LogInformation("User {UserId} disconnected from SignalR hub", userId);
        }

        // Removes user from group
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            if (_faceScope.IsAvailable)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, FaceChatBroadcastGroup(_faceScope.FaceId));
        }

        // Calls base implementation
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends message to all connected clients
    /// 
    /// This method can be called from client using:
    /// await connection.InvokeAsync("SendMessage", "Username", "Message text");
    /// </summary>
    /// <param name="user">Name of user sending the message</param>
    /// <param name="message">Message text</param>
    public async Task SendMessage(string user, string message)
    {
        if (!_faceScope.IsAvailable)
            return;

        // Gets sender User ID
        var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;

        _logger.LogInformation("User {UserId} sent message: {Message}", userId, message);

        // Tenant-scoped broadcast (ACL G14): never fan out across URL face prefixes.
        await Clients.Group(FaceChatBroadcastGroup(_faceScope.FaceId)).SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Sends private message to specific user
    /// 
    /// This method can be called from client using:
    /// await connection.InvokeAsync("SendPrivateMessage", "targetUserId", "Message text");
    /// </summary>
    /// <param name="targetUserId">User ID of message recipient (from JWT token - ClaimTypes.NameIdentifier)</param>
    /// <param name="message">Message text</param>
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

        // Sends message only to specific user
        // Clients.User() finds all connections of given user and sends message to all of them
        // Client can listen on "ReceivePrivateMessage" callback:
        // connection.On("ReceivePrivateMessage", (string sender, string message) => { ... });
        await Clients.User(targetUserId).SendAsync("ReceivePrivateMessage", senderId, message);
    }

    /// <summary>
    /// Sends user message to AI service (Python) via gRPC Generate RPC and returns AI response to caller.
    /// Optional history builds conversation context so the model can "remember" previous messages in this session.
    /// Client can call: await connection.InvokeAsync("SendToAi", "user message", historyArray);
    /// Client listens: connection.On("ReceiveAiMessage", (string userMessage, string aiResponse) => { ... });
    /// </summary>
    /// <param name="message">User message (prompt) to send to the AI.</param>
    /// <param name="history">Optional list of previous user/AI message pairs for conversation context.</param>
    public async Task SendToAi(string message, ChatHistoryEntry[]? history = null)
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} sent message to AI: {Message}", userId, message);

        // ACL A20: bound gRPC cost per user; authenticated hub only — still need abuse limits for shared AI backend.
        if (!_aiRateLimiter.TryAllow(userId))
        {
            await Clients.Caller.SendAsync(
                "ReceiveAiMessage",
                message ?? string.Empty,
                "You are sending too many AI requests. Please wait a moment and try again.");
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
    /// Platform-operator AI chat with optional public aggregate statistics (<paramref name="statsMode"/>:
    /// <c>off</c>, <c>inline</c> — JSON from DB, <c>live</c> — Python HTTP-fetches configured public URL).
    /// </summary>
    public async Task SendToAiWithOperatorStats(string message, ChatHistoryEntry[]? history, string? statsMode)
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation(
            "User {UserId} sent operator stats AI message (mode {Mode})",
            userId,
            statsMode ?? "off");

        if (!CanManageAllFaces())
        {
            await Clients.Caller.SendAsync(
                "ReceiveAiMessage",
                message ?? string.Empty,
                "Statistics-aware AI is only available to platform operators (admin face scope + manage-all-faces capability).");
            return;
        }

        if (!_aiRateLimiter.TryAllow(userId))
        {
            await Clients.Caller.SendAsync(
                "ReceiveAiMessage",
                message ?? string.Empty,
                "You are sending too many AI requests. Please wait a moment and try again.");
            return;
        }

        var mode = (statsMode ?? "off").Trim().ToLowerInvariant();
        if (mode is not ("off" or "inline" or "live"))
            mode = "off";

        string aiResponse;
        try
        {
            var prompt = BuildPromptWithHistory(message ?? string.Empty, history);
            if (mode == "inline")
            {
                var snapshot = await _platformStats.GetPublicSnapshotAsync(Context.ConnectionAborted);
                var json = JsonSerializer.Serialize(snapshot, PublicStatsJsonOptions);
                aiResponse = await _aiGrpcService.GenerateAsync(
                    prompt,
                    maxNewTokens: 150,
                    statsContextJson: json,
                    cancellationToken: Context.ConnectionAborted);
            }
            else if (mode == "live")
            {
                var liveUrl = (_configuration["AiStats:PublicSnapshotAbsoluteUrl"] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(liveUrl))
                {
                    aiResponse =
                        "Live statistics mode is not configured on the server (AiStats:PublicSnapshotAbsoluteUrl). Use inline mode or contact an administrator.";
                }
                else
                {
                    var historyText = BuildHistoryPlainText(history);
                    aiResponse = await _aiGrpcService.OperatorStatsChatAsync(
                        message ?? string.Empty,
                        historyText,
                        fetchLivePublicSnapshot: true,
                        publicStatsAbsoluteUrl: liveUrl,
                        maxNewTokens: 150,
                        cancellationToken: Context.ConnectionAborted);
                }
            }
            else
                aiResponse = await _aiGrpcService.GenerateAsync(prompt, maxNewTokens: 150, cancellationToken: Context.ConnectionAborted);

            if (string.IsNullOrWhiteSpace(aiResponse))
                aiResponse = "...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendToAiWithOperatorStats failed for user {UserId}", userId);
            aiResponse = "Error: AI service is currently unavailable. Please try again later.";
        }

        await Clients.Caller.SendAsync("ReceiveAiMessage", message ?? string.Empty, aiResponse);
    }

    /// <summary>
    /// Builds prompt string from optional history and current user message so the model sees conversation context.
    /// </summary>
    private static string BuildPromptWithHistory(string message, ChatHistoryEntry[]? history)
    {
        var sb = new System.Text.StringBuilder();
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
