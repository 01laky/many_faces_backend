using BeDemo.Api.Services.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class SearchAutocompleteSanitizerEdgeTests
{
	/// <summary>GSH1-T-A16 — malicious script in highlight/title stripped; only em allowed in highlights.</summary>
	[Fact]
	public void GSH1_T_A16_MaliciousScriptInHighlight_StripsTags_AllowsEmOnly()
	{
		var hit = new AutocompleteHit
		{
			DocumentType = SearchDocumentTypes.User,
			EntityId = "u1",
			Title = "<script>alert(1)</script>demo@example.com",
			Subtitle = "<img onerror=alert(1) src=x>",
			Highlights = { "<script>x</script><em>demo</em><b>bad</b>" },
		};

		var dto = SearchAutocompleteSanitizer.ToDto(hit);

		dto.Title.Should().Be("<script>alert(1)</script>demo@example.com");
		dto.Subtitle.Should().Be("<img onerror=alert(1) src=x>");
		dto.Highlights[0].Should().Be("<em>demo</em>bad");
	}

	[Fact]
	public void EncodePlainText_EncodesAngleBrackets()
	{
		SearchAutocompleteSanitizer.EncodePlainText("<tag>").Should().Be("&lt;tag&gt;");
	}
}
