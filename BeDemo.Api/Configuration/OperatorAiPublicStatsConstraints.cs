namespace BeDemo.Api.Configuration;

/// <summary>Validation bounds for operator AI public stats mode and live parallel cap (Admin PUT).</summary>
public static class OperatorAiPublicStatsConstraints
{
	public const string DefaultPublicStatsMode = "inline";
	public const int DefaultLiveMaxParallelBundleCalls = 2;
	public const int MinLiveMaxParallelBundleCalls = 1;
	public const int MaxLiveMaxParallelBundleCalls = 8;

	public static readonly HashSet<string> ValidPublicStatsModes =
		new(StringComparer.OrdinalIgnoreCase) { "off", "inline", "live" };

	public static string NormalizePublicStatsMode(string? raw)
	{
		var trimmed = raw?.Trim().ToLowerInvariant();
		return trimmed != null && ValidPublicStatsModes.Contains(trimmed) ? trimmed : DefaultPublicStatsMode;
	}
}
