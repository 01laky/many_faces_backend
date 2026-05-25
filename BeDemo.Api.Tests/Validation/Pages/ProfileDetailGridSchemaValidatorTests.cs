using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Validation.Pages;
using FluentAssertions;

namespace BeDemo.Api.Tests.Validation.Pages;

public sealed class ProfileDetailGridSchemaValidatorTests
{
	[Fact]
	public void Validate_accepts_default_seed_json()
	{
		ProfileDetailGridSchemaValidator.Validate(ProfileDetailGridDefaults.DefaultGridSchemaJson)
			.Should().BeNull();
	}

	[Fact]
	public void Validate_rejects_unknown_section_type()
	{
		var json = ProfileDetailGridDefaults.DefaultGridSchemaJson.Replace(
			"\"profileHero\"",
			"\"notASection\"",
			StringComparison.Ordinal);
		ProfileDetailGridSchemaValidator.Validate(json)
			.Should().Contain("sectionType");
	}

	[Fact]
	public void Validate_rejects_duplicate_item_ids()
	{
		var json = """
            {"items":[{"i":"a","sectionType":"profileBackNav"},{"i":"a","sectionType":"spacer"}]}
            """;
		ProfileDetailGridSchemaValidator.Validate(json)
			.Should().Contain("Duplicate");
	}

	[Theory]
	[InlineData(null, "required")]
	[InlineData("", "required")]
	[InlineData("   ", "required")]
	[InlineData("{not-json", "valid JSON")]
	[InlineData("[]", "root must be an object")]
	[InlineData("{}", "items must be an array")]
	[InlineData("""{"items":{}}""", "items must be an array")]
	[InlineData("""{"schemaVersion":2,"items":[]}""", "Unsupported schemaVersion")]
	[InlineData("""{"schemaVersion":1.5,"items":[]}""", "Unsupported schemaVersion")]
	[InlineData("""{"schemaVersion":999999999999,"items":[]}""", "Unsupported schemaVersion")]
	[InlineData("""{"items":["not-object"]}""", "Each grid item must be an object")]
	[InlineData("""{"items":[{"sectionType":"profileHero"}]}""", "requires string property i")]
	[InlineData("""{"items":[{"i":" ","sectionType":"profileHero"}]}""", "must be non-empty")]
	[InlineData("""{"items":[{"i":"hero"}]}""", "requires sectionType")]
	[InlineData("""{"items":[{"i":"hero","sectionType":" "}]}""", "Unknown or missing sectionType")]
	public void Validate_rejects_malformed_or_unsupported_grid_shapes(string? json, string expectedMessagePart)
	{
		ProfileDetailGridSchemaValidator.Validate(json)
			.Should().Contain(expectedMessagePart);
	}

	[Fact]
	public void Validate_rejects_payloads_over_size_limit()
	{
		var oversizedJson = $$"""
            {"items":[{"i":"{{new string('a', 64 * 1024)}}","sectionType":"profileHero"}]}
            """;

		ProfileDetailGridSchemaValidator.Validate(oversizedJson)
			.Should().Contain("maximum allowed size");
	}

	[Fact]
	public void Validate_rejects_more_than_maximum_items()
	{
		var items = string.Join(
			',',
			Enumerable.Range(0, 41).Select(i => $$"""{"i":"item-{{i}}","sectionType":"spacer"}"""));
		var json = $$"""{"items":[{{items}}]}""";

		ProfileDetailGridSchemaValidator.Validate(json)
			.Should().Contain("maximum of 40");
	}
}
