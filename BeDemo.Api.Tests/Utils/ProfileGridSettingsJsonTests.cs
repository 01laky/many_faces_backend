using System.Text.Json.Nodes;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the profile grid-settings JSON merge (previously untested): tolerant parse of
/// stored settings, a deep merge where a null patch value removes a key, and the size-limit guard.
/// </summary>
public sealed class ProfileGridSettingsJsonTests
{
	[Fact]
	public void TryParseResponse_treats_null_or_empty_as_empty_components()
	{
		ProfileGridSettingsJson.TryParseResponse(null, out var components, out var error).Should().BeTrue();
		components.Count.Should().Be(0);
		error.Should().BeNull();

		ProfileGridSettingsJson.TryParseResponse("   ", out components, out _).Should().BeTrue();
		components.Count.Should().Be(0);
	}

	[Fact]
	public void TryParseResponse_extracts_grid_components()
	{
		var ok = ProfileGridSettingsJson.TryParseResponse(
			"{\"gridComponents\":{\"x\":{\"autoplay\":true}}}",
			out var components,
			out _);

		ok.Should().BeTrue();
		components.ContainsKey("x").Should().BeTrue();
	}

	[Fact]
	public void TryParseResponse_returns_error_for_invalid_json()
	{
		ProfileGridSettingsJson.TryParseResponse("{not json", out _, out var error).Should().BeFalse();
		error.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void TryParseResponse_without_grid_components_is_empty_and_ok()
	{
		ProfileGridSettingsJson.TryParseResponse("{\"other\":1}", out var components, out _).Should().BeTrue();
		components.Count.Should().Be(0);
	}

	[Fact]
	public void TryMergePatch_adds_and_null_removes_keys()
	{
		var patch = new JsonObject
		{
			["a"] = null,
			["c"] = new JsonObject { ["autoplay"] = false },
		};

		var ok = ProfileGridSettingsJson.TryMergePatch(
			"{\"gridComponents\":{\"a\":{\"autoplay\":true},\"b\":{\"autoplay\":true}}}",
			patch,
			out var merged,
			out var error);

		ok.Should().BeTrue();
		error.Should().BeNull();

		var root = JsonNode.Parse(merged)!.AsObject();
		var components = root["gridComponents"]!.AsObject();
		components.ContainsKey("a").Should().BeFalse(); // removed by null patch
		components.ContainsKey("b").Should().BeTrue(); // preserved
		components.ContainsKey("c").Should().BeTrue(); // added
	}

	[Fact]
	public void TryMergePatch_rejects_an_oversized_payload()
	{
		var patch = new JsonObject { ["big"] = JsonValue.Create(new string('x', 20_000)) };

		var ok = ProfileGridSettingsJson.TryMergePatch(null, patch, out var merged, out var error);

		ok.Should().BeFalse();
		merged.Should().BeEmpty();
		error.Should().NotBeNullOrEmpty();
	}
}
