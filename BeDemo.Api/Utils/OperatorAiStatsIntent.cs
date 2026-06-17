namespace BeDemo.Api.Utils;

/// <summary>
/// Heuristic: should operator AI receive DB statistics JSON for this user message?
/// </summary>
public static class OperatorAiStatsIntent
{
	private static readonly string[] MetricsKeywords =
	[
		"koľko", "kolko", "koľk", "kolik", "how many", "how much", "count", "počet", "pocet",
		"štatistik", "statistik", "statistics", "stats", "metrics", "metrik", "dashboard",
		"users", "userov", "používateľ", "pouzivatel", "správ", "sprav", "messages",
		"registr", "friend", "priateľ", "album", "blog", "reel", "story", "stories",
		"wall", "ticket", "chat room", "chatroom", "oauth", "moderation", "many faces", "mfai",
		"platform", "databáz", "databaz", "database", "celkom", "spolu", "total",
		"včera", "vcera", "týždeň", "tyzden", "week", "trend", "timeseries", "graf",
		"information", "informations", "informácie", "informacie", "overview", "prehľad", "prehlad",
		"summary", "súhrn", "suhrn", "results", "výsled", "vysled", "report",
		"give me", "show me", "tell me about", "info about", "data about",
	];

	private static readonly string[] BroadOverviewKeywords =
	[
		"all information", "all informations", "all data", "all stats", "all statistics",
		"everything in", "whole system", "entire system", "full overview", "complete overview",
		// Operator phrasings that previously slipped through (full-stats broad-overview fix): "give me full
		// statistics", "all entities results", "complete stats", … plus their Slovak variants.
		"full statistics", "full stats", "all entities", "all entity", "all the stats",
		"complete statistics", "complete stats", "every entity",
		// "Give me full entities statistics" / "entity statistics" phrasings the operator actually used.
		"full entities", "full entity", "entities statistics", "entity statistics", "entities stats",
		"všetky inform", "vsetky inform", "všetko v systéme", "vsetko v systeme",
		"celý systém", "cely system", "platform overview", "system overview",
		"úplné štatistiky", "uplne statistiky", "kompletné štatistiky", "kompletne statistiky",
		"všetky entity", "vsetky entity", "celé štatistiky", "cele statistiky",
	];

	private static readonly string[] NonMetricsKeywords =
	[
		"koľko je hodín", "kolko je hodin", "what time", "aký je čas", "aky je cas",
		"system date", "dátum a čas", "datum a cas", "explain signalr", "ako funguje kód",
		"write code", "napíš kód", "napis kod", "refactor", "typescript", "react",
	];

	public static bool IsMetricsQuestion(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		var m = message.Trim().ToLowerInvariant();

		foreach (var phrase in NonMetricsKeywords)
		{
			if (m.Contains(phrase, StringComparison.Ordinal))
				return false;
		}

		if (IsBroadOverviewQuestion(message))
			return true;

		foreach (var word in MetricsKeywords)
		{
			if (m.Contains(word, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	// 7B-perf O2 — a single-metric COUNT/total ask whose answer is a number already in the bundle, so the LLM
	// adds nothing and we can answer deterministically (0 generations). DELIBERATELY STRICT: a false negative just
	// runs the normal LLM path (no harm); a false positive returns a templated answer where nuance was needed
	// (worse), so anything comparative / trend / qualitative / breakdown disqualifies and we bias to false.

	private static readonly string[] CountTriggers =
	[
		"how many", "how much", "number of", "total number", "count of", "count",
		"koľko", "kolko", "počet", "pocet",
	];

	private static readonly string[] CountDisqualifiers =
	[
		// comparative
		"compare", "comparison", " vs ", " vs.", "versus", "difference between", "more than", "less than",
		// trend / time
		"trend", "over time", "growth", "rate", "per day", "per week", "last week", "last month",
		"yesterday", "today", "this week", "timeseries", "graph", "chart",
		// qualitative / reasoning
		"why", "how come", "explain", "reason", "should", "recommend", "best", "worst", "healthy", "problem",
		// breakdown / grouping (needs structure, not a single number)
		"breakdown", "break down", "group by", "grouped", " by ", "per ", "each ", "distribution",
		"average", "median", "percentage", "percent", "ratio", "list", "list all", "show all", "every",
		// Slovak — the count triggers (koľko / počet) are bilingual, so the disqualifiers must be too:
		// average / why / compare / breakdown-by / trend + time windows.
		"priemer", "prečo", "preco", "porovn", "podľa", "podla", "rozdelen", "vývoj", "vyvoj", "rast",
		"týžd", "tyzd", "mesiac", "za posledn", "minul", "pribud",
	];

	/// <summary>
	/// 7B-perf O2 — true only for a genuinely simple single-metric count/total question (e.g. "how many users",
	/// "albums pending count"). Strict by design; returns false for anything comparative, trend-based, qualitative,
	/// or that asks for a breakdown/overview. Used to take the deterministic count fast-path (no Generate).
	/// </summary>
	public static bool IsSimpleCountQuestion(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		var m = message.Trim().ToLowerInvariant();

		// A broad overview is never a "simple count" — it spans many metrics.
		if (IsBroadOverviewQuestion(message))
			return false;

		// Must look like a metrics question at all.
		if (!IsMetricsQuestion(message))
			return false;

		// Any disqualifier ⇒ defer to the LLM.
		foreach (var phrase in CountDisqualifiers)
		{
			if (m.Contains(phrase, StringComparison.Ordinal))
				return false;
		}

		// Must contain an explicit count/total trigger.
		foreach (var trigger in CountTriggers)
		{
			if (m.Contains(trigger, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	/// <summary>User wants a wide platform snapshot (use compact all-bundle overview, not 4 bundle picks).</summary>
	public static bool IsBroadOverviewQuestion(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		var m = message.Trim().ToLowerInvariant();
		foreach (var phrase in BroadOverviewKeywords)
		{
			if (m.Contains(phrase, StringComparison.Ordinal))
				return true;
		}

		// operator-ai conversational-context + broad-overview fix (B1): the free `"all " + (system|platform)`
		// fallback was DELETED. It mis-fired on focused follow-ups that merely happen to contain both words —
		// e.g. "…if our reels in system are all active now" ("all " + "system") — and dumped all 61 bundles.
		// Legitimate whole-platform phrasings are already covered explicitly by BroadOverviewKeywords above;
		// novel phrasings the list misses are PROMOTED by the 3B upgrade (OperatorAiDecisionHelper.IsBroadOverviewAsync),
		// and when the helper is off they degrade to a FOCUSED answer (a narrower answer, never a dump) — acceptable.
		return false;
	}

	/// <summary>
	/// operator-ai conversational-context fix — true when the message hits the deterministic NON-metrics list
	/// ("what time", "explain signalr", "write code", "ako funguje kód", …). The follow-up resolver's rung-3
	/// anaphora gate consults this to turn entity-carry OFF for how-to / code / time questions, so a stale entity
	/// is never prepended onto a non-statistical turn.
	/// </summary>
	public static bool ContainsNonMetricsKeyword(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		var m = message.Trim().ToLowerInvariant();
		foreach (var phrase in NonMetricsKeywords)
		{
			if (m.Contains(phrase, StringComparison.Ordinal))
				return true;
		}

		return false;
	}
}
