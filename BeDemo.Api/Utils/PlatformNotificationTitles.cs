namespace BeDemo.Api.Utils;

/// <summary>Localized notification titles for platform DMs (recipient-facing; best-effort culture).</summary>
public static class PlatformNotificationTitles
{
	/// <summary>Title for <c>super_admin_message</c> notifications.</summary>
	public static string SuperAdminMessage(string? cultureCode)
	{
		var c = cultureCode?.Trim().ToLowerInvariant();
		return c switch
		{
			"sk" => "Správa od administrátora platformy",
			"cz" => "Zpráva od administrátora platformy",
			_ => "Platform administrator",
		};
	}
}
