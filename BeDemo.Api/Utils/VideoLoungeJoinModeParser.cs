using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class VideoLoungeJoinModeParser
{
	public static bool TryParseMemberMode(string? value, out VideoLoungeJoinMode mode)
	{
		mode = default;
		if (string.IsNullOrWhiteSpace(value))
			return false;

		return Enum.TryParse(value.Trim(), ignoreCase: true, out mode)
			&& mode is VideoLoungeJoinMode.Viewer or VideoLoungeJoinMode.Listener or VideoLoungeJoinMode.Full;
	}

	public static bool IsOperatorStealth(VideoLoungeJoinMode mode) => mode == VideoLoungeJoinMode.AdminStealth;
}
