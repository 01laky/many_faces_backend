namespace BeDemo.Api.Services;

/// <summary>
/// Parser for the red-team <c>prompt_injection_corpus.txt</c> file (shipped with <c>BeDemo.Api.Tests</c>).
/// </summary>
/// <remarks>
/// Each non-comment line is a synthetic attack string. Tests assert that
/// <see cref="ContentModerationUntrustedContentEvaluator"/> never yields
/// <see cref="AiReviewStatus.RecommendedApprove"/> when the AI returns high-confidence
/// <see cref="AiReviewDecision.Approve"/> for content containing that line.
/// </remarks>
public static class ContentModerationPromptInjectionCorpus
{
    /// <summary>Minimum corpus size required by the moderation prompt-injection defense spec.</summary>
    public const int MinimumLineCount = 20;

    /// <summary>
    /// Parses corpus text: blank lines and <c>#</c> comments are ignored; other lines are attack payloads.
    /// </summary>
    public static IReadOnlyList<string> ParseLines(string raw)
    {
        var lines = new List<string>();
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            lines.Add(trimmed);
        }

        return lines;
    }
}
