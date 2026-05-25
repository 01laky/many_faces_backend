using System.Text;
using System.Text.Json;
using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Build compact platform snapshot JSON from prefetched bundle cache (overview path).</summary>
public static class OperatorAiLiveStatsOverview
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	public static string BuildCompactJson(IReadOnlyDictionary<int, OperatorAiBundleCacheEntry> cache)
	{
		var rows = new List<object>();
		foreach (var index in Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount))
		{
			if (!cache.TryGetValue(index, out var entry)
				|| entry.State != OperatorAiBundleCacheState.Ready
				|| string.IsNullOrEmpty(entry.JsonPayload))
				continue;

			try
			{
				var dto = JsonSerializer.Deserialize<OperatorAiEntityBundleDto>(entry.JsonPayload, JsonOptions);
				if (dto == null)
					continue;

				rows.Add(new
				{
					dto.BundleId,
					dto.TotalCount,
					dto.ByStatus,
					dto.ByType,
				});
			}
			catch (JsonException)
			{
				// Skip malformed cache row.
			}
		}

		var payload = new
		{
			snapshotUtc = DateTime.UtcNow,
			bundleCount = rows.Count,
			bundles = rows,
		};
		return JsonSerializer.Serialize(payload, JsonOptions);
	}
}
