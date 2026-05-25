using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.Requests.Profile;

public class UpdateProfileRequest
{
	public string? FirstName { get; set; }
	public string? LastName { get; set; }

	[JsonPropertyName("enableAnimatedGradient")]
	public bool? EnableAnimatedGradient { get; set; }

	[JsonPropertyName("preferredUiLanguage")]
	public string? PreferredUiLanguage { get; set; }

	[JsonPropertyName("lastSelectedFaceId")]
	public int? LastSelectedFaceId { get; set; }

	/// <summary>When true, clears <see cref="PreferredUiLanguage"/>.</summary>
	[JsonPropertyName("clearPreferredUiLanguage")]
	public bool ClearPreferredUiLanguage { get; set; }

	/// <summary>When true, clears <see cref="LastSelectedFaceId"/>.</summary>
	[JsonPropertyName("clearLastSelectedFaceId")]
	public bool ClearLastSelectedFaceId { get; set; }
}

