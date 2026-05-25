using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>Public live roster DTOs — stealth participants excluded from member-facing fields.</summary>
public static class VideoLoungeLiveSnapshot
{
	public sealed class ParticipantRow
	{
		public required string UserId { get; init; }
		public required string DisplayName { get; init; }
		public string? AvatarUrl { get; init; }
		public required string JoinMode { get; init; }
		public bool AudioEnabled { get; init; }
		public bool VideoEnabled { get; init; }
	}

	public static object BuildPublic(
		IReadOnlyList<FaceVideoLoungeSessionParticipant> active,
		IReadOnlyDictionary<string, (string DisplayName, string? AvatarUrl)> users)
	{
		var listed = active.Where(p => p.IsListedInPublicRoster && p.LeftAt == null).ToList();
		var liveParticipants = listed.Select(p =>
		{
			users.TryGetValue(p.UserId, out var u);
			return new ParticipantRow
			{
				UserId = p.UserId,
				DisplayName = u.DisplayName,
				AvatarUrl = u.AvatarUrl,
				JoinMode = p.JoinMode.ToString(),
				AudioEnabled = p.AudioEnabled,
				VideoEnabled = p.VideoEnabled,
			};
		}).ToList();

		var viewerCount = listed.Count(p => p.JoinMode == VideoLoungeJoinMode.Viewer);
		var speakerCount = listed.Count(p =>
			p.JoinMode is VideoLoungeJoinMode.Listener or VideoLoungeJoinMode.Full);

		return new
		{
			hasLiveSession = true,
			liveParticipantCount = listed.Count,
			liveViewerCount = viewerCount,
			liveSpeakerCount = speakerCount,
			liveParticipants,
		};
	}

	public static object BuildOperator(
		IReadOnlyList<FaceVideoLoungeSessionParticipant> active,
		IReadOnlyDictionary<string, (string DisplayName, string? AvatarUrl)> users)
	{
		var all = active.Where(p => p.LeftAt == null).ToList();
		var operatorLiveParticipants = all.Select(p =>
		{
			users.TryGetValue(p.UserId, out var u);
			return new
			{
				userId = p.UserId,
				displayName = u.DisplayName,
				avatarUrl = u.AvatarUrl,
				joinMode = p.JoinMode.ToString(),
				p.AudioEnabled,
				p.VideoEnabled,
				p.IsListedInPublicRoster,
			};
		}).ToList();

		var listed = all.Where(p => p.IsListedInPublicRoster).ToList();
		var liveParticipants = listed.Select(p =>
		{
			users.TryGetValue(p.UserId, out var u);
			return new ParticipantRow
			{
				UserId = p.UserId,
				DisplayName = u.DisplayName,
				AvatarUrl = u.AvatarUrl,
				JoinMode = p.JoinMode.ToString(),
				AudioEnabled = p.AudioEnabled,
				VideoEnabled = p.VideoEnabled,
			};
		}).ToList();

		return new
		{
			hasLiveSession = true,
			liveParticipantCount = listed.Count,
			liveViewerCount = listed.Count(p => p.JoinMode == VideoLoungeJoinMode.Viewer),
			liveSpeakerCount = listed.Count(p =>
				p.JoinMode is VideoLoungeJoinMode.Listener or VideoLoungeJoinMode.Full),
			liveParticipants,
			operatorLiveParticipants,
		};
	}
}
