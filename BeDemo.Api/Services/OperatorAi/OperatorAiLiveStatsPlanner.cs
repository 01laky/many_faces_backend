using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stage 2 — build planner prompt and parse index JSON from model output.</summary>
public static class OperatorAiLiveStatsPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex JsonFenceRegex = new(
        @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string BuildPrompt(string userMessage, OperatorAiBundleCatalogDto catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a routing assistant. Given the user question and the bundle catalog below,");
        sb.AppendLine("return ONLY JSON: {\"indices\":[...],\"reason\":\"...\"}.");
        sb.AppendLine("Pick the smallest set of bundle indices needed to answer. Do not answer the user yet.");
        sb.AppendLine();
        sb.AppendLine("User question:");
        sb.AppendLine(userMessage.Trim());
        sb.AppendLine();
        sb.AppendLine("Bundle catalog:");
        foreach (var b in catalog.Bundles)
            sb.AppendLine($"{b.Index} — {b.Id} — {b.Description}");
        sb.AppendLine();
        sb.AppendLine("JSON:");
        return sb.ToString();
    }

    public static OperatorAiLivePlannerResultDto ParseIndices(
        string modelOutput,
        int catalogLength,
        int maxSelected,
        bool metricsLike)
    {
        var indices = TryExtractIndices(modelOutput, catalogLength);
        if (indices.Count == 0)
            indices = metricsLike ? [0] : [];

        if (indices.Count > maxSelected)
            indices = indices.Take(maxSelected).ToList();

        return new OperatorAiLivePlannerResultDto
        {
            Indices = indices,
            Reason = null,
        };
    }

    private static List<int> TryExtractIndices(string modelOutput, int catalogLength)
    {
        if (string.IsNullOrWhiteSpace(modelOutput))
            return [];

        var jsonText = modelOutput.Trim();
        var fence = JsonFenceRegex.Match(jsonText);
        if (fence.Success)
            jsonText = fence.Groups[1].Value;

        var brace = jsonText.IndexOf('{');
        if (brace >= 0)
        {
            var end = jsonText.LastIndexOf('}');
            if (end > brace)
                jsonText = jsonText[brace..(end + 1)];
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!doc.RootElement.TryGetProperty("indices", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [0];

            var set = new HashSet<int>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var idx))
                    continue;
                if (idx < 0 || idx >= catalogLength)
                    continue;
                set.Add(idx);
            }

            return set.Count == 0 ? [] : set.OrderBy(i => i).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>Stage 5 — deterministic stitch of per-bundle sub-answers (v1 manual glue).</summary>
public static class OperatorAiLiveStatsStitch
{
    public sealed record Part(int Index, string BundleId, string Text, bool Failed);

    public static string Stitch(IReadOnlyList<Part> parts)
    {
        if (parts.Count == 0)
            return "No statistics data was available to answer this question.";

        var blocks = new List<string>();
        foreach (var part in parts.OrderBy(p => p.Index))
        {
            if (part.Failed)
            {
                blocks.Add($"**{part.BundleId}:** Data unavailable for this bundle.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(part.Text))
            {
                blocks.Add($"**{part.BundleId}:** No answer generated.");
                continue;
            }

            blocks.Add($"**{part.BundleId}:**\n{part.Text.Trim()}");
        }

        return string.Join("\n\n", blocks).Trim();
    }
}
