using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeDemo.Api.Utils;

/// <summary>Merge grid component UI prefs into UserFaceProfile.Settings JSON.</summary>
public static class ProfileGridSettingsJson
{
	public const int MaxBytes = 16 * 1024;

	public static bool TryParseResponse(string? settingsJson, out JsonObject gridComponents, out string? error)
	{
		gridComponents = new JsonObject();
		error = null;
		if (string.IsNullOrWhiteSpace(settingsJson))
			return true;

		try
		{
			var root = JsonNode.Parse(settingsJson) as JsonObject;
			if (root?["gridComponents"] is JsonObject components)
			{
				gridComponents = components.DeepClone() as JsonObject ?? new JsonObject();
			}

			return true;
		}
		catch (JsonException)
		{
			error = "Invalid stored settings JSON";
			return false;
		}
	}

	public static bool TryMergePatch(string? existingJson, JsonObject patchComponents, out string merged, out string? error)
	{
		merged = string.Empty;
		error = null;

		if (!TryParseResponse(existingJson, out var existing, out error))
			return false;

		foreach (var (key, value) in patchComponents)
		{
			if (value is null)
				existing.Remove(key);
			else
				existing[key] = value.DeepClone();
		}

		var root = new JsonObject { ["gridComponents"] = existing };
		merged = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

		if (System.Text.Encoding.UTF8.GetByteCount(merged) > MaxBytes)
		{
			error = "Settings payload too large";
			merged = string.Empty;
			return false;
		}

		return true;
	}
}
