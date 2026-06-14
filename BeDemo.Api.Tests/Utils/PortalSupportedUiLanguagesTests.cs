using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the portal UI-language allow-list (previously untested): the six supported codes
/// are accepted case-insensitively and trimmed; nullish/whitespace/unknown codes are rejected.
/// </summary>
public sealed class PortalSupportedUiLanguagesTests
{
	[Theory]
	[InlineData("en")]
	[InlineData("sk")]
	[InlineData("cz")]
	[InlineData("de")]
	[InlineData("fr")]
	[InlineData("it")]
	[InlineData("EN")]
	[InlineData("  it  ")]
	public void IsAllowed_accepts_supported_codes_case_insensitively(string code)
	{
		PortalSupportedUiLanguages.IsAllowed(code).Should().BeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("xx")]
	[InlineData("english")]
	[InlineData("cs")] // .NET culture token, not the portal "cz" UI code
	public void IsAllowed_rejects_nullish_and_unknown_codes(string? code)
	{
		PortalSupportedUiLanguages.IsAllowed(code).Should().BeFalse();
	}
}
