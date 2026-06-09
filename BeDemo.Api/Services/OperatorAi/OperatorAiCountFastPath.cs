using System.Globalization;
using System.Text;
using System.Text.Json;
using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// 7B-perf O2 — deterministic count fast-path formatter. For a genuinely simple single-metric count question over a
/// single bundle, the answer is a number that is already present in the freshly-loaded bundle JSON, so we format a
/// short templated reply with NO <c>Generate</c> call at all. Figures come straight from the JSON (thousands-
/// formatted); nothing is invented. Returns <c>null</c> when the JSON has no usable <c>totalCount</c> — the caller
/// then falls through to the normal LLM map path (bias to the LLM, never a wrong templated number).
/// </summary>
public static class OperatorAiCountFastPath
{
	/// <summary>
	/// Try to render a deterministic count line for <paramref name="bundleJson"/>. Returns null when the bundle has
	/// no <c>totalCount</c> (⇒ caller should fall back to the LLM).
	/// </summary>
	public static string? TryFormat(OperatorAiBundleCatalogEntryDto meta, string? bundleJson)
	{
		if (string.IsNullOrWhiteSpace(bundleJson))
			return null;

		long total;
		string? breakdown;
		try
		{
			using var doc = JsonDocument.Parse(bundleJson);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				return null;
			if (!TryReadLong(root, "totalCount", out total))
				return null;

			// A single small status/type breakdown makes the answer richer but stays fully deterministic.
			breakdown = FormatBreakdown(root, "byStatus") ?? FormatBreakdown(root, "byType");
		}
		catch (JsonException)
		{
			return null;
		}

		var label = LabelFor(meta);
		var sb = new StringBuilder();
		sb.Append("**").Append(label).Append(":** ").Append(total.ToString("N0", CultureInfo.InvariantCulture)).Append(" total");
		if (!string.IsNullOrEmpty(breakdown))
			sb.Append(" — ").Append(breakdown);
		sb.Append('.');
		return sb.ToString();
	}

	/// <summary>Human label for the bundle (e.g. "Albums") derived from the catalog id; deterministic, no AI.</summary>
	private static string LabelFor(OperatorAiBundleCatalogEntryDto meta)
	{
		var id = meta.Id ?? "Data";
		// Bundle ids look like "albums" / "albums.byStatus" — take the leading segment and title-case it.
		var head = id.Split('.', '_', '-')[0].Trim();
		if (head.Length == 0)
			return "Data";
		return char.ToUpperInvariant(head[0]) + head[1..];
	}

	private static bool TryReadLong(JsonElement obj, string name, out long value)
	{
		value = 0;
		if (!obj.TryGetProperty(name, out var el))
			return false;
		switch (el.ValueKind)
		{
			case JsonValueKind.Number when el.TryGetInt64(out var n):
				value = n;
				return true;
			case JsonValueKind.String when long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s):
				value = s;
				return true;
			default:
				return false;
		}
	}

	/// <summary>Format an object like {"approved":100,"pending":20} as "100 approved, 20 pending"; null when absent/empty.</summary>
	private static string? FormatBreakdown(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
			return null;

		var parts = new List<string>();
		foreach (var prop in el.EnumerateObject())
		{
			if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var n))
				parts.Add($"{n.ToString("N0", CultureInfo.InvariantCulture)} {prop.Name}");
		}
		return parts.Count > 0 ? string.Join(", ", parts) : null;
	}
}
