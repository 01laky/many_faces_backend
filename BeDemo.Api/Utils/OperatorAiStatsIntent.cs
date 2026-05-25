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
		"všetky inform", "vsetky inform", "všetko v systéme", "vsetko v systeme",
		"celý systém", "cely system", "platform overview", "system overview",
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

		// "all ... in system" / "all ... in the platform"
		if (m.Contains("all ", StringComparison.Ordinal)
			&& (m.Contains("system", StringComparison.Ordinal) || m.Contains("platform", StringComparison.Ordinal)))
			return true;

		return false;
	}
}
