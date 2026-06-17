using System.Linq;
using System.Text;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// operator-ai conversational-context + broad-overview fix — shared, deterministic, WORD-BOUNDARY entity
/// detection over the authored per-bundle synonyms (<see cref="OperatorAiKnowledgeDescriptors.ByIndex"/>).
///
/// <para>Why a dedicated util / why word-boundary:</para>
/// The synonym map was authored for embedding <c>content_text</c>, NOT for substring matching, so a naive
/// <c>message.Contains(synonym)</c> false-matches ("reel" ∈ "f<b>reel</b>y", "wall" ∈ "<b>wall</b>et",
/// "story" ∈ "hi<b>story</b>"). We therefore NORMALIZE the message (lowercase; every run of non-alphanumeric
/// characters → a single space; trimmed) and require the synonym — itself normalized and space-wrapped — to
/// appear as a whole token/phrase (" reels ", " short videos "). Diacritics are preserved (Slovak input stays
/// intact); the authored synonyms are English, which is what we match against.
///
/// <para>Single source of truth (no inverted layering):</para>
/// BOTH the follow-up resolver (rung-2 "own entity" detection + the per-conversation memo) and
/// <see cref="OperatorAiDecisionHelper.IsBroadOverviewAsync"/> (the flagless single-entity broad-suppress) call
/// THIS util. It depends only on the descriptor map, never on the higher-level <c>OperatorAiFollowUpResolver</c>,
/// so the layering is not inverted and there is one detection algorithm, not two divergent copies.
/// </summary>
public static class OperatorAiEntityDetection
{
	/// <summary>
	/// Per-bundle detection metadata, built once from the descriptor map. <c>PrimarySynonym</c> is the bundle's
	/// first authored synonym (the canonical entity noun, e.g. "reels") used by the resolver's memo + prepend;
	/// <c>WrappedSynonyms</c> are the space-wrapped normal forms (" reels ") so a single Contains is a word match.
	/// </summary>
	private static readonly IReadOnlyList<(int Index, string PrimarySynonym, string[] WrappedSynonyms)> Bundles = BuildBundles();

	private static IReadOnlyList<(int, string, string[])> BuildBundles()
	{
		var list = new List<(int, string, string[])>();
		foreach (var kvp in OperatorAiKnowledgeDescriptors.ByIndex)
		{
			var synonyms = kvp.Value.Synonyms;
			if (synonyms is null || synonyms.Length == 0)
				continue;

			var primary = Normalize(synonyms[0]);
			if (primary.Length == 0)
				continue;

			var wrapped = new List<string>(synonyms.Length);
			foreach (var s in synonyms)
			{
				var n = Normalize(s);
				if (n.Length > 0)
					wrapped.Add(" " + n + " ");
			}

			if (wrapped.Count > 0)
				list.Add((kvp.Key, primary, wrapped.ToArray()));
		}

		return list;
	}

	/// <summary>
	/// The DISTINCT catalog bundle indices whose synonyms appear as whole tokens/phrases in <paramref name="message"/>.
	/// Empty when none match. Overlapping synonyms across bundles naturally yield 2+ indices, which callers read as
	/// "not a single entity" (the realistic common outcome — §3 rung-2 note in the spec).
	/// </summary>
	public static IReadOnlyCollection<int> DetectEntityBundleIndices(string? message)
	{
		var matched = new HashSet<int>();
		if (string.IsNullOrWhiteSpace(message))
			return matched;

		// Space-wrap the normalized haystack so the first/last token is matchable by " token " too.
		var haystack = " " + Normalize(message) + " ";
		foreach (var (index, _, wrapped) in Bundles)
		{
			foreach (var syn in wrapped)
			{
				if (haystack.Contains(syn, StringComparison.Ordinal))
				{
					matched.Add(index);
					break; // one hit is enough to mark this bundle present
				}
			}
		}

		return matched;
	}

	/// <summary>
	/// When the message names EXACTLY ONE bundle, return that bundle's primary synonym (e.g. "reels"); otherwise
	/// null. <paramref name="entityCount"/> returns the distinct-bundle count (0 / 1 / 2+) so callers can separate
	/// rung-2 (exactly one → focus + remember) from rung-3 (exactly zero → maybe carry) from a self-contained
	/// multi-entity message (2+ → never carry).
	/// </summary>
	public static string? SingleEntityPrimarySynonym(string? message, out int entityCount)
	{
		var indices = DetectEntityBundleIndices(message);
		entityCount = indices.Count;
		if (entityCount != 1)
			return null;

		var only = indices.First();
		foreach (var (index, primary, _) in Bundles)
		{
			if (index == only)
				return primary;
		}

		return null;
	}

	/// <summary>
	/// Normalize for word-boundary matching: lowercase, every run of non-alphanumeric characters collapses to a
	/// single space, leading/trailing space trimmed. Letters with diacritics are kept (they are alphanumeric), so
	/// Slovak input is not mangled. Shared with the resolver's anaphora-marker matching so both use one normal form.
	/// </summary>
	internal static string Normalize(string text)
	{
		var sb = new StringBuilder(text.Length);
		var lastWasSpace = true; // start "spaced" so any leading separators are trimmed
		foreach (var ch in text)
		{
			if (char.IsLetterOrDigit(ch))
			{
				sb.Append(char.ToLowerInvariant(ch));
				lastWasSpace = false;
			}
			else if (!lastWasSpace)
			{
				sb.Append(' ');
				lastWasSpace = true;
			}
		}

		// Trim a possible trailing space left by the final separator run.
		return sb.ToString().TrimEnd();
	}
}
