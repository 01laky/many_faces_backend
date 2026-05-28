namespace BeDemo.Api.Services.Grid;

/// <summary>Supported <c>blocks=</c> keys for <see cref="IFaceGridSnapshotService"/> (portal grid registry).</summary>
public static class GridBlockKeys
{
	public const string Albums = "albums";
	public const string Blogs = "blogs";
	public const string Reels = "reels";
	public const string Stories = "stories";
	public const string ChatRooms = "chat-rooms";
	public const string Profiles = "profiles";
	public const string WallTickets = "wall-tickets";

	public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		Albums,
		Blogs,
		Reels,
		Stories,
		ChatRooms,
		Profiles,
		WallTickets,
	};

	/// <summary>Blocks that require an authenticated user (same as individual list endpoints).</summary>
	public static readonly IReadOnlySet<string> AuthRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		Albums,
		Blogs,
		Reels,
		Stories,
		ChatRooms,
		WallTickets,
	};

	/// <summary>Parses comma-separated block keys: dedupes, lowercases, ignores unknown keys (BE-RP8-U2/U4).</summary>
	public static IReadOnlyList<string> ParseBlocks(string? blocksParam)
	{
		if (string.IsNullOrWhiteSpace(blocksParam))
			return Array.Empty<string>();

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();
		foreach (var raw in blocksParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!All.Contains(raw))
				continue;

			var normalized = raw.ToLowerInvariant();
			if (seen.Add(normalized))
				result.Add(normalized);
		}

		return result;
	}
}
