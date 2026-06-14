using System.Text.Json;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the per-face gradient presets (previously untested): the seeded faces get their
/// fixed palette, unknown indices get a deterministic hash-derived variant (stable + case-insensitive), and
/// the output is always a valid gradient JSON shape.
/// </summary>
public sealed class FaceGradientPresetsTests
{
	private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

	[Fact]
	public void Known_face_index_returns_its_fixed_preset()
	{
		var root = Parse(FaceGradientPresets.JsonForFaceIndex("public"));
		root.GetProperty("type").GetString().Should().Be("linear");
		root.GetProperty("colors").EnumerateArray().Select(c => c.GetString())
			.Should().Equal("#6366f1", "#06b6d4", "#a78bfa");
		root.GetProperty("angle").GetInt32().Should().Be(118);
		root.GetProperty("animation").GetString().Should().Be("rotate");
	}

	[Fact]
	public void Known_face_index_is_case_insensitive()
	{
		FaceGradientPresets.JsonForFaceIndex("PUBLIC")
			.Should().Be(FaceGradientPresets.JsonForFaceIndex("public"));
	}

	[Fact]
	public void Unknown_index_is_deterministic_across_calls()
	{
		FaceGradientPresets.JsonForFaceIndex("some-new-face")
			.Should().Be(FaceGradientPresets.JsonForFaceIndex("some-new-face"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("zzz-unknown")]
	public void Always_produces_a_valid_gradient_shape(string? faceIndex)
	{
		var root = Parse(FaceGradientPresets.JsonForFaceIndex(faceIndex));
		root.GetProperty("type").GetString().Should().Be("linear");
		root.GetProperty("colors").GetArrayLength().Should().BeGreaterThan(0);
		root.TryGetProperty("animation", out _).Should().BeTrue();
		root.TryGetProperty("animationSpeed", out _).Should().BeTrue();
	}
}
