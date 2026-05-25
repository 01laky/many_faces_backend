namespace BeDemo.Api.Utils;

/// <summary>
/// Shared upload size limits and user-facing error fragments (SHV2 BE-U2 — message matches enforced byte cap).
/// </summary>
public static class UploadLimits
{
	/// <summary>Profile / face avatar uploads (<see cref="Controllers.ProfileController"/>).</summary>
	public const int AvatarMaxBytes = 30 * 1024 * 1024;

	/// <summary>Story image multipart limit is enforced by <c>[RequestSizeLimit]</c> on the action; keep message in sync if changed.</summary>
	public const long StoryImageMaxBytes = 52L * 1024 * 1024;

	/// <summary>Human-readable cap for API error JSON (derived from byte limit, not a separate product rule).</summary>
	public static string FormatMaxFileSizeMessage(int maxBytes) =>
		$"File too large. Max {maxBytes / (1024 * 1024)} MB.";
}
