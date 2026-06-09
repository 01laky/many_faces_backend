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
		// Backend-refactor §5 fix: dropped the bare "grpc" marker — it false-positived any legitimate answer
		// mentioning gRPC (e.g. an operator asking the AI about the gRPC workers). Real transport failures still
		// surface via the "Error:" prefix and the "ai service unavailable/timed out" markers above.
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

		// Backend-refactor §5 REVIEW: these Slovak markers are a locale leak vs the D10 English-only decision, but a
		// test relies on them and the worker historically emitted them — keep them (defensive) pending a coordinated
		// worker change; only the unsafe bare-"grpc" infrastructure marker was removed.
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
			// After the RAG refactor (D11) there is no stats-mode to switch to — the chat is always
			// data-grounded. English-only message (D10), with no reference to the removed `inline` mode.
			return "Sorry — the request could not be processed right now (statistics or AI service). "
				+ "Please try again shortly.";
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
