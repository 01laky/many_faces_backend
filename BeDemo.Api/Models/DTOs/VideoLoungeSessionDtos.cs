namespace BeDemo.Api.Models.DTOs;

/// <summary>Response for join-live, refresh-token: token + routing params.</summary>
public sealed class VideoLoungeJoinResultDto
{
	public int SessionId { get; init; }
	public string JoinMode { get; init; } = string.Empty;
	public string Token { get; init; } = string.Empty;
	public string ServerUrl { get; init; } = string.Empty;
	public string RoomName { get; init; } = string.Empty;
	public bool IsStub { get; init; }
	public DateTime ExpiresAtUtc { get; init; }
}

/// <summary>Refresh-token only response (no sessionId/joinMode).</summary>
public sealed class VideoLoungeRefreshTokenResultDto
{
	public string Token { get; init; } = string.Empty;
	public string ServerUrl { get; init; } = string.Empty;
	public string RoomName { get; init; } = string.Empty;
	public bool IsStub { get; init; }
	public DateTime ExpiresAtUtc { get; init; }
}

/// <summary>Returned when no live session is active (empty live snapshot).</summary>
public sealed class VideoLoungeNoSessionDto
{
	public bool HasLiveSession { get; init; } = false;
	public int LiveParticipantCount { get; init; } = 0;
	public int LiveViewerCount { get; init; } = 0;
	public int LiveSpeakerCount { get; init; } = 0;
	public IEnumerable<object> LiveParticipants { get; init; } = [];
}
