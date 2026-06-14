using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for localized platform-DM notification titles (previously untested): sk/cz map to
/// their localized strings, everything else (null, unknown culture, casing/whitespace) falls back to English.
/// </summary>
public sealed class PlatformNotificationTitlesTests
{
	[Theory]
	[InlineData("sk", "Správa od administrátora platformy")]
	[InlineData("SK", "Správa od administrátora platformy")]
	[InlineData("  cz  ", "Zpráva od administrátora platformy")]
	public void SuperAdminMessage_localizes_known_cultures(string culture, string expected)
	{
		PlatformNotificationTitles.SuperAdminMessage(culture).Should().Be(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("en")]
	[InlineData("de")]
	[InlineData("xx")]
	public void SuperAdminMessage_falls_back_to_english(string? culture)
	{
		PlatformNotificationTitles.SuperAdminMessage(culture).Should().Be("Platform administrator");
	}
}
