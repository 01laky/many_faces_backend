using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace BeDemo.Api.Tests.Grid;

internal static class GridGoldenFixturePaths
{
	public static string MemberSnapshot =>
		Path.Combine(AppContext.BaseDirectory, "Fixtures", "Grid", "grid-snapshot-member-public.json");

	public static string GuestProfilesSnapshot =>
		Path.Combine(AppContext.BaseDirectory, "Fixtures", "Grid", "grid-snapshot-guest-profiles-public.json");
}

internal static class GridJsonComparer
{
	private static readonly JsonSerializerOptions SerializeOptions = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	public static JsonNode Parse(string json) =>
		JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON is empty.");

	public static string ToCanonicalJson(JsonNode node) =>
		JsonSerializer.Serialize(node, SerializeOptions);

	public static bool DeepEquals(JsonNode? a, JsonNode? b, out string? difference)
	{
		difference = null;
		var aJson = a?.ToJsonString() ?? "null";
		var bJson = b?.ToJsonString() ?? "null";
		if (aJson == bJson)
			return true;

		difference = $"Expected: {ToCanonicalJson(Parse(bJson))}{Environment.NewLine}Actual:   {ToCanonicalJson(Parse(aJson))}";
		return false;
	}
}

internal static class GridGoldenSchemaAssert
{
	public static void PaginatedEnvelopeMatchesSchema(JsonNode actual)
	{
		foreach (var key in new[] { "items", "page", "pageSize", "totalCount", "totalPages" })
			actual[key].Should().NotBeNull($"paginated envelope missing '{key}'");
	}

	public static void ProfilesEnvelopeMatchesSchema(JsonElement actual, JsonNode goldenSchema)
	{
		foreach (var key in new[] { "items", "page", "pageSize", "totalCount", "totalPages" })
			actual.TryGetProperty(key, out _).Should().BeTrue($"profiles envelope missing '{key}'");

		actual.GetProperty("page").GetInt32().Should().Be(goldenSchema["page"]?.GetValue<int>() ?? 1);
		actual.GetProperty("pageSize").GetInt32().Should().Be(goldenSchema["pageSize"]?.GetValue<int>() ?? 10);

		if (actual.GetProperty("items").GetArrayLength() > 0)
		{
			var item = actual.GetProperty("items")[0];
			foreach (var field in new[] { "userId", "displayName", "avatarUrl", "commentsCount", "likesCount", "reviewsCount", "isFaceBanned" })
				item.TryGetProperty(field, out _).Should().BeTrue($"profile item missing '{field}'");
		}
	}
}
