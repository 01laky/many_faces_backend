namespace BeDemo.Api.Hubs;

/// <summary>
/// Filters infrastructure / gRPC / fetch failures so they are not stored as assistant chat turns.
/// </summary>
internal static class OperatorAiResponseGuard
{
	private static readonly string[] InfrastructureMarkers =
	[
		"urlopen error",
		"<urlopen",
		"connection refused",
		"errno 111",
		"ai service unavailable",
		"ai service timed out",
		"error: ai service",
		"public_stats_absolute_url",
		"grpc",
	];

	/// <summary>True when the text is a technical failure, not a real assistant reply.</summary>
	public static bool IsInfrastructureFailure(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var t = text.Trim();
		if (t.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
			return true;

		foreach (var marker in InfrastructureMarkers)
		{
			if (t.Contains(marker, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>Model-loading / availability placeholders (also not persisted).</summary>
	public static bool IsTransientStatusMessage(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return false;

		return text.Contains("načítava", StringComparison.OrdinalIgnoreCase)
			|| text.Contains("nacitava", StringComparison.OrdinalIgnoreCase)
			|| text.Contains("MODEL_LOAD", StringComparison.OrdinalIgnoreCase)
			|| text.Contains("AI služba nie je dostupná", StringComparison.OrdinalIgnoreCase);
	}

	public static bool ShouldNotPersist(string? text) =>
		IsTransientStatusMessage(text) || IsInfrastructureFailure(text);

	public static string ToUserFacingMessage(string? raw)
	{
		if (IsInfrastructureFailure(raw))
		{
			return "Ospravedlňujem sa, momentálne sa nepodarilo spracovať požiadavku (štatistiky alebo AI služba). "
				+ "Skúste znova o chvíľu alebo v Nastaveniach prepnite štatistiky na „inline“.";
		}

		if (IsTransientStatusMessage(raw))
			return raw!.Trim();

		return SanitizeAssistantText(raw);
	}

	private static string SanitizeAssistantText(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return "...";

		var text = raw.Trim();
		const string namePrefix = "MFAI Assistant:";
		if (text.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
			text = text[namePrefix.Length..].TrimStart();

		return string.IsNullOrWhiteSpace(text) ? "..." : text;
	}
}
