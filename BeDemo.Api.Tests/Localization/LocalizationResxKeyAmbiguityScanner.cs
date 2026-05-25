using System.Globalization;
using System.Resources;
using BeDemo.Api.Localization;

namespace BeDemo.Api.Tests.Localization;

/// <summary>
/// Loads flat <c>.resx</c> key sets per app/culture and reports prefix conflicts that break JSON unflattening.
/// </summary>
/// <remarks>
/// Used by <see cref="ResxLocalizationKeyAmbiguityTests"/> and mirrored in CI via
/// <c>scripts/verify-localization-key-parity.mjs</c> (dotnet test filter).
/// </remarks>
internal static class LocalizationResxKeyAmbiguityScanner
{
	/// <summary>
	/// Cultures stored on disk as satellite <c>.resx</c> files (API still exposes <c>cz</c> from <c>cs</c>).
	/// </summary>
	public static readonly CultureInfo[] ResxCultures =
	[
		CultureInfo.GetCultureInfo("en"),
		CultureInfo.GetCultureInfo("sk"),
		CultureInfo.GetCultureInfo("cs"),
	];

	/// <summary>
	/// Enumerates every flat key in the resource manager for a culture.
	/// </summary>
	public static HashSet<string> ReadFlatKeys(ResourceManager rm, CultureInfo culture)
	{
		var set = rm.GetResourceSet(culture, true, true)
			?? throw new InvalidOperationException($"Missing resource set for culture '{culture.Name}'.");

		var keys = new HashSet<string>(StringComparer.Ordinal);
		foreach (System.Collections.DictionaryEntry entry in set)
		{
			if (entry.Key is string k)
				keys.Add(k);
		}

		return keys;
	}

	/// <summary>
	/// Returns ambiguous (parent, child) pairs for one app culture, or empty when safe.
	/// </summary>
	public static IReadOnlyList<(string Parent, string Child)> FindConflicts(
		ResourceManager rm,
		CultureInfo culture)
	{
		var keys = ReadFlatKeys(rm, culture);
		var conflicts = ResourceJsonUnflattener.FindAmbiguousFlatKeys(keys);
		return conflicts;
	}

	/// <summary>
	/// Builds a single assertion message listing all conflicts for an app/culture.
	/// </summary>
	public static string FormatConflictMessage(
		string appLabel,
		string cultureName,
		IReadOnlyList<(string Parent, string Child)> conflicts)
	{
		if (conflicts.Count == 0)
			return string.Empty;

		var lines = conflicts
			.Select(c => $"  - '{c.Parent}' conflicts with '{c.Child}' (remove one or rename)")
			.ToList();

		return $"""
            {appLabel} ({cultureName}) has {conflicts.Count} ambiguous .resx key pair(s).
            A key cannot be both a leaf value and a parent path (ResourceJsonUnflattener will throw).
            {string.Join(Environment.NewLine, lines)}
            """;
	}
}
