namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// operator-ai degraded failure-handling fix — single source of truth for "this generation result is actually a
/// failure, not an answer".
///
/// <para>Why this exists:</para>
/// <see cref="IAiGrpcService.GenerateAsync"/> does NOT throw on a transport / model failure — it RETURNS a string
/// prefixed <c>"Error: …"</c> (e.g. <c>"Error: AI service unavailable (Unavailable)"</c>), or
/// <c>"AI support is currently disabled …"</c> when the global switch is off. Callers that forget this record the
/// error string as if it were a successful answer; in the per-bundle map path that error text then leaked into the
/// stitch + synthesis prompt and the 7B narrated it ("…the AI service is currently unavailable…"). Both the
/// per-bundle map path and the terminal generation now funnel through this helper so the convention is checked in
/// exactly one place.
/// </summary>
public static class OperatorAiGenerationErrors
{
	/// <summary>True when a <see cref="IAiGrpcService.GenerateAsync"/> result is the error-string sentinel, not an answer.</summary>
	public static bool IsErrorText(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var t = text.TrimStart();
		return t.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
			|| t.StartsWith("AI support is currently disabled", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>True when the result is unusable — either an error sentinel or empty/whitespace.</summary>
	public static bool IsUnusable(string? text) => string.IsNullOrWhiteSpace(text) || IsErrorText(text);
}
