using System.Collections.Concurrent;
using BeDemo.Api.Configuration;
using BeDemo.Api.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// operator-ai conversational-context + broad-overview fix (A1) — resolves a possibly-anaphoric operator follow-up
/// into a self-contained query string by carrying the last named entity, so the otherwise-stateless operator-AI
/// turn can answer "All active?" after "how many reels?".
/// </summary>
public interface IOperatorAiFollowUpResolver
{
	/// <summary>
	/// Run the decision ladder for one turn and return the query the router / RAG / broad-check should see. Either
	/// the original message (unchanged) or, at rung 3, the original prepended with the carried entity noun (e.g.
	/// "All active?" → "reels All active?"). Side effect: updates the per-conversation memo when the message names
	/// exactly one entity. The CALLER still persists/display the ORIGINAL message — only routing sees the result.
	/// </summary>
	string Resolve(string userMessage, int conversationId);
}

/// <summary>
/// Fully deterministic follow-up resolver — NO model, no <see cref="IOperatorAiDecisionHelper"/> dependency (that is
/// the whole point of A1: predictable behaviour, zero added latency, works with the 3B off).
///
/// <para>Decision ladder (top-down, first match wins — see the spec §3):</para>
/// <list type="number">
///   <item><b>Own entity?</b> the message names exactly one bundle (word-boundary synonym) → remember it (the
///   rung-2 signal feeds the memo) and pass through unchanged (the entity is already in the text). The memo write
///   is independent of which skill runs or whether a stats answer results — it tracks "the last concrete entity the
///   operator mentioned", a stable anchor for the next anaphor.</item>
///   <item><b>Broad?</b> a whole-platform overview (deterministic <see cref="OperatorAiStatsIntent.IsBroadOverviewQuestion"/>)
///   never carries → pass through unchanged. (We use the DETERMINISTIC check here, not the 3B
///   <c>IsBroadOverviewAsync</c>: the downstream stats skill still does the 3B upgrade, and the conservative rung-3
///   gate would not admit a novel broad phrasing anyway — so a model call here would be a needless dependency.)</item>
///   <item><b>Anaphoric follow-up?</b> exactly ZERO entities, not broad, AND the conservative positive gate
///   (<see cref="IsAnaphoricFollowUp"/>) holds → prepend the remembered entity. No memo ⇒ pass through.</item>
///   <item><b>Else</b> → pass through unchanged (normal routing downstream).</item>
/// </list>
///
/// <para>Rung-3 precondition is EXACTLY ZERO entities — stricter than "rung 2 (exactly-one) failed": a message
/// naming TWO entities also fails rung 2 but is self-contained and must NOT carry. So a 2-entity message falls
/// straight through to rung 4 instead of relying on the marker gate to reject it.</para>
///
/// <para>The per-conversation memo is in-memory singleton state (consistent with the existing answer / vector
/// caches). It is per-instance — under multi-instance hosting a follow-up served by another instance won't see it;
/// that assumes sticky sessions, same as today's caches.</para>
/// </summary>
public sealed class OperatorAiFollowUpResolver : IOperatorAiFollowUpResolver
{
	/// <summary>Anaphors are short — the antecedent is the carried entity, not this sentence. Above this word count we do not carry.</summary>
	private const int MaxFollowUpWords = 6;

	// ── Referential markers (bilingual sk/en) for the rung-3 positive gate ───────────────────────
	// All forms are in the OperatorAiEntityDetection.Normalize() shape (lowercase, diacritics kept, single spaces).
	// We deliberately OMIT collision-prone tokens: the English article "a" / preposition "to" / demonstrative
	// "that" are too common to treat as anaphora markers, so the Slovak cases ("a aktívne?", "tie?") are covered by
	// the pronoun + quantifier/status markers instead.

	/// <summary>Message LEADS with one of these (a continuation of the previous turn).</summary>
	private static readonly string[] LeadingMarkers =
	[
		"and ", "what about", "how about", // en
		"a čo", "a co", "čo ", "co ", "a tie", // sk
	];

	/// <summary>A referential pronoun appears as a whole token.</summary>
	private static readonly string[] PronounMarkers =
	[
		"it", "they", "them", "those", "these", // en
		"tie", "tých", "tych", "ony", "ich", // sk
	];

	/// <summary>A bare quantifier / status word with no entity (e.g. "all active?", "len schválené?").</summary>
	private static readonly string[] QuantifierStatusMarkers =
	[
		"all", "just", "only", "any", "every", "pending", "approved", "active", "rejected", "removed", // en
		"všetky", "vsetky", "všetko", "vsetko", "len", "iba", "aktívne", "aktivne", "schválené", "schvalene",
		"zamietnuté", "zamietnute", "čakajúce", "cakajuce", "odstránené", "odstranene", // sk
	];

	private readonly ConcurrentDictionary<int, string> _lastEntityByConversation = new();
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiFollowUpResolver> _logger;

	public OperatorAiFollowUpResolver(
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiFollowUpResolver> logger)
	{
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public string Resolve(string userMessage, int conversationId)
	{
		var trimmed = (userMessage ?? string.Empty).Trim();
		if (!_options.FollowUpEntityCarryEnabled || trimmed.Length == 0)
			return trimmed;

		// Rung 2 — the message names exactly one entity: remember it and pass through (the entity is already there).
		var primary = OperatorAiEntityDetection.SingleEntityPrimarySynonym(trimmed, out var entityCount);
		if (primary is not null)
		{
			_lastEntityByConversation[conversationId] = primary;
			return trimmed;
		}

		// Rung 1 — a broad (whole-platform) request never carries.
		if (OperatorAiStatsIntent.IsBroadOverviewQuestion(trimmed))
			return trimmed;

		// Rung 3 precondition (c) — EXACTLY ZERO entities. A 2+ entity message is self-contained → rung 4, no carry.
		if (entityCount != 0)
			return trimmed;

		// Rung 3 — anaphoric follow-up: carry the remembered entity ONLY when the conservative positive gate holds.
		if (_lastEntityByConversation.TryGetValue(conversationId, out var carried)
			&& IsAnaphoricFollowUp(trimmed))
		{
			_logger.LogInformation(
				"Operator AI follow-up carry: prepended entity '{Entity}' (conversation {ConversationId}).",
				carried,
				conversationId);
			return carried + " " + trimmed;
		}

		// Rung 4 — pass through unchanged.
		return trimmed;
	}

	/// <summary>
	/// The rung-3 POSITIVE gate (§3): a message qualifies as an anaphoric follow-up only when ALL hold — (d) it has
	/// no NON-metrics keyword (how-to / code / time turn carry off), (b) it is short (≤ <see cref="MaxFollowUpWords"/>
	/// words), and (a) a referential marker is present (leading conjunction, pronoun, or a bare quantifier/status
	/// word). This is deliberately NOT the naive "not broad ∧ no entity ∧ short" — that would prepend a stale entity
	/// onto a genuinely new turn ("how do I add a face?"). Callers must have already checked "not broad" and
	/// "exactly zero entities" before calling this.
	/// </summary>
	internal static bool IsAnaphoricFollowUp(string trimmed)
	{
		// (d) how-to / code / time questions are never follow-up metrics anaphors.
		if (OperatorAiStatsIntent.ContainsNonMetricsKeyword(trimmed))
			return false;

		// (b) length cap.
		if (CountWords(trimmed) > MaxFollowUpWords)
			return false;

		// (a) a referential marker must be present (shared normal form with entity detection).
		var norm = OperatorAiEntityDetection.Normalize(trimmed);
		if (norm.Length == 0)
			return false;

		foreach (var lead in LeadingMarkers)
		{
			if (norm.StartsWith(lead, StringComparison.Ordinal))
				return true;
		}

		var wrapped = " " + norm + " ";
		foreach (var pronoun in PronounMarkers)
		{
			if (wrapped.Contains(" " + pronoun + " ", StringComparison.Ordinal))
				return true;
		}

		foreach (var marker in QuantifierStatusMarkers)
		{
			if (wrapped.Contains(" " + marker + " ", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static int CountWords(string trimmed)
	{
		var count = 0;
		var inWord = false;
		foreach (var ch in trimmed)
		{
			if (char.IsWhiteSpace(ch))
			{
				inWord = false;
			}
			else if (!inWord)
			{
				inWord = true;
				count++;
			}
		}

		return count;
	}
}
