using System.Text.Json;

namespace BeDemo.Api.Tests;

/// <summary>
/// Helper methods for working with JSON in tests
/// </summary>
public static class TestJsonHelpers
{
	/// <summary>
	/// Get string property from JsonElement
	/// </summary>
	public static string GetStringProperty(this JsonElement element, string propertyName)
	{
		return element.GetProperty(propertyName).GetString() ?? string.Empty;
	}

	/// <summary>
	/// Get int property from JsonElement
	/// </summary>
	public static int GetIntProperty(this JsonElement element, string propertyName)
	{
		return element.GetProperty(propertyName).GetInt32();
	}

	/// <summary>
	/// Get property value or default if not present
	/// </summary>
	public static string? GetStringPropertyOrDefault(this JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var prop))
			return prop.GetString();
		return null;
	}
}
