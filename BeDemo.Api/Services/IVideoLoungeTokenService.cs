using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>Issues SFU access tokens per <see cref="VideoLoungeJoinMode"/> grants.</summary>
public interface IVideoLoungeTokenService
{
	/// <summary>
	/// Builds a room token for the given session participant.
	/// Viewer/AdminStealth: subscribe only; Listener: audio publish; Full: audio+video publish.
	/// </summary>
	VideoLoungeTokenResult CreateToken(
		int sessionId,
		string userId,
		string displayName,
		VideoLoungeJoinMode joinMode);
}

public sealed class VideoLoungeTokenResult
{
	public required string Token { get; init; }
	public required string ServerUrl { get; init; }
	public required string RoomName { get; init; }
	public bool IsStub { get; init; }
	public DateTime ExpiresAtUtc { get; init; }
}
