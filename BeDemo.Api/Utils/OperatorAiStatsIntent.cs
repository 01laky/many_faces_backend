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
        "wall", "ticket", "chat room", "oauth", "moderation", "many faces", "mfai",
        "platform", "databáz", "databaz", "database", "celkom", "spolu", "total",
        "včera", "vcera", "týždeň", "tyzden", "week", "trend", "timeseries", "graf",
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

        foreach (var word in MetricsKeywords)
        {
            if (m.Contains(word, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
