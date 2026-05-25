/*
 * ChatHistoryEntry.cs - DTO for one user/AI message pair in chat history
 *
 * Used by SignalR client when calling SendToAi with conversation context.
 * Client sends camelCase (userMessage, aiResponse); this type deserializes from JSON.
 */

using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// One pair of user message and AI response for conversation context.
/// </summary>
public class ChatHistoryEntry
{
	[JsonPropertyName("userMessage")]
	public string UserMessage { get; set; } = string.Empty;

	[JsonPropertyName("aiResponse")]
	public string AiResponse { get; set; } = string.Empty;
}
