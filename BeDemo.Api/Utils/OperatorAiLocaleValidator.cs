namespace BeDemo.Api.Utils;

public static class OperatorAiLocaleValidator
{
	private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
	{
		"en",
		"sk",
		"cz",
	};

	public static bool TryNormalize(string? input, out string normalized)
	{
		normalized = string.Empty;
		if (string.IsNullOrWhiteSpace(input))
			return false;

		var lower = input.Trim().ToLowerInvariant();
		if (!Allowed.Contains(lower))
			return false;

		normalized = lower;
		return true;
	}
}
