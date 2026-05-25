using FluentAssertions;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>SHV2 PI-8: plain-text moderation preview helpers.</summary>
[Trait(ContentModerationCiGate.XunitTraitName, ContentModerationCiGate.XunitTraitCategory)]
[Trait(ContentModerationCiGate.XunitTraitName, ContentModerationCiGate.XunitTraitCategory)]
public sealed class ContentModerationPreviewTextTests
{
	[Fact]
	public void ToPlainTextPreview_strips_script_tags_and_decodes_entities()
	{
		var html = "<p>Hello <b>world</b> &amp; &#39;friends&#39;</p><script>alert(1)</script>";
		var plain = ContentModerationPreviewText.ToPlainTextPreview(html);
		plain.Should().NotContain("<").And.NotContain(">").And.NotContain("script");
		plain.Should().Contain("Hello").And.Contain("world").And.Contain("'");
	}

	[Fact]
	public void ToMediaUrlPreview_returns_null_for_whitespace()
	{
		ContentModerationPreviewText.ToMediaUrlPreview("   ").Should().BeNull();
	}
}
