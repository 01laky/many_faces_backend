using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BeDemo.Api.Tests;

/// <summary>Helpers for list endpoints that return { items, page, pageSize, totalCount, totalPages }.</summary>
public static class IntegrationTestPaginatedList
{
	public static JsonElement[] ReadItems(JsonElement envelope)
	{
		envelope.ValueKind.Should().Be(JsonValueKind.Object);
		envelope.TryGetProperty("items", out var items).Should().BeTrue();
		items.ValueKind.Should().Be(JsonValueKind.Array);
		return items.EnumerateArray().ToArray();
	}

	public static async Task<JsonElement[]> GetListItemsAsync(HttpClient client, string url)
	{
		var envelope = await client.GetFromJsonAsync<JsonElement>(url);
		return ReadItems(envelope);
	}
}
