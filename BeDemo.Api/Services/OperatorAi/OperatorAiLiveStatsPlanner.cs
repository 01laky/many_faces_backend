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

	/// <summary>Merge keyword-derived bundle indices so planner misses (e.g. users + chat rooms) are covered.</summary>
	public static IReadOnlyList<int> SupplementIndicesFromMessage(
		string userMessage,
		IReadOnlyList<int> plannerIndices,
		int catalogLength,
		int maxSelected)
	{
		var merged = new List<int>(plannerIndices);
		var m = userMessage.Trim().ToLowerInvariant();

		foreach (var (keywords, indices) in KeywordRoutes)
		{
			if (!keywords.Any(k => m.Contains(k, StringComparison.Ordinal)))
				continue;

			foreach (var idx in indices)
			{
				if (idx >= 0 && idx < catalogLength && !merged.Contains(idx))
					merged.Add(idx);
			}
		}

		merged.Sort();
		return merged.Count <= maxSelected ? merged : merged.Take(maxSelected).ToList();
	}

	private static readonly (string[] Keywords, int[] Indices)[] KeywordRoutes =
	[
		(["user", "users", "userov", "používateľ", "pouzivatel"], [0]),
		(["chat room", "chatroom", "chat rooms"], [42, 43, 44]),
		(["message", "messages", "správ", "sprav", "dm "], [10, 44]),
		(["friend", "priateľ", "priatel"], [6, 7]),
		(["face", "faces", "tvár", "tvar", "tenant"], [12]),
		(["page", "pages", "cms"], [13]),
		(["album", "albums"], [23]),
		(["blog", "blogs"], [28]),
		(["reel", "reels"], [32]),
		(["story", "stories"], [36]),
		(["video lounge", "lounge"], [46, 49]),
		(["wall ticket", "wall tickets"], [51]),
		(["moderation", "moderate"], [54, 55, 56]),
		(["notification"], [11]),
	];

	public static string BuildSynthesisPrompt(
		string userMessage,
		string draftAnswer,
		string responseLocale)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are the MFAI Demo operator dashboard assistant.");
		sb.AppendLine("Synthesize ONE clear, helpful answer for the operator using ONLY the facts below.");
		sb.AppendLine("Use exact numbers. Do not ask the user to attach JSON. No greeting.");
		sb.AppendLine($"Reply in {responseLocale}.");
		sb.AppendLine();
		sb.AppendLine("Operator question:");
		sb.AppendLine(userMessage.Trim());
		sb.AppendLine();
		sb.AppendLine("Facts from database bundles:");
		sb.AppendLine(draftAnswer.Trim());
		sb.AppendLine();
		sb.AppendLine("AI:");
		return sb.ToString();
	}

	public static string BuildOverviewPrompt(
		string userMessage,
		string overviewJson,
		string responseLocale)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are the MFAI Demo operator dashboard assistant.");
		sb.AppendLine("The JSON below is an aggregate snapshot of ALL entity counts in the platform.");
		sb.AppendLine("Summarize the most important metrics for the operator. Use exact numbers.");
		sb.AppendLine("Group by area (users, social, content, chat, moderation). No greeting.");
		sb.AppendLine($"Reply in {responseLocale}.");
		sb.AppendLine();
		sb.AppendLine("Operator question:");
		sb.AppendLine(userMessage.Trim());
		sb.AppendLine();
		sb.AppendLine("Platform snapshot JSON:");
		sb.AppendLine(overviewJson);
		sb.AppendLine();
		sb.AppendLine("AI:");
		return sb.ToString();
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
	// operator-ai degraded-handling — AiError distinguishes a MODEL failure (Generate returned an "Error:" string,
	// timed out, or threw) from a DATA failure (the bundle's DB JSON was not ready). Only a model failure means
	// "AI unavailable"; a not-ready bundle is a data gap that still renders a clean per-section "data unavailable" note.
	public sealed record Part(int Index, string BundleId, string Text, bool Failed, bool AiError = false);

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
