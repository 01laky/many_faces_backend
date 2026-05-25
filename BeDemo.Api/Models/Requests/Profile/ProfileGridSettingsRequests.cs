using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.Requests.Profile;

public sealed class GridComponentPreferenceEntry
{
	public bool? Autoplay { get; set; }
}

public sealed class UpdateFaceGridSettingsRequest
{
	[JsonPropertyName("gridComponents")]
	public Dictionary<string, GridComponentPreferenceEntry>? GridComponents { get; set; }
}
