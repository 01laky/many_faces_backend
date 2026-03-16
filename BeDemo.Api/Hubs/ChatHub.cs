/*
 * ChatHub.cs - SignalR Hub for real-time chat communication
 * 
 * This hub provides WebSocket endpoint for real-time communication.
 * All methods require authentication ([Authorize] attribute).
 * 
 * Endpoint: wss://localhost:8001/hubs/chat?access_token=<JWT_TOKEN>
 * 
 * Methods:
 * - SendMessage: Sends message to all connected clients
 * - SendPrivateMessage: Sends private message to specific user
 * - SendToAi: Sends user message to AI service via gRPC, returns AI response to caller (ReceiveAiMessage)
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Hubs;

/// <summary>
/// SignalR Hub for chat communication
/// [Authorize] ensures that only authenticated users can access the hub
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IAiGrpcService _aiGrpcService;

    public ChatHub(ILogger<ChatHub> logger, IAiGrpcService aiGrpcService)
    {
        _logger = logger;
        _aiGrpcService = aiGrpcService;
    }

    /// <summary>
    /// Invoked automatically when client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
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
        // Gets sender User ID
        var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;

        _logger.LogInformation("User {UserId} sent message: {Message}", userId, message);

        // Sends message to all connected clients
        // Clients can listen on "ReceiveMessage" callback:
        // connection.On("ReceiveMessage", (string user, string message) => { ... });
        await Clients.All.SendAsync("ReceiveMessage", user, message);
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
        // Gets sender User ID
        var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;

        _logger.LogInformation("User {UserId} sent private message to {TargetUserId}", userId, targetUserId);

        // Sends message only to specific user
        // Clients.User() finds all connections of given user and sends message to all of them
        // Client can listen on "ReceivePrivateMessage" callback:
        // connection.On("ReceivePrivateMessage", (string sender, string message) => { ... });
        await Clients.User(targetUserId).SendAsync("ReceivePrivateMessage", userId, message);
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
}
