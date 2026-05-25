using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeDemo.Api.Tests.Localization;

/// <summary>
/// Extracts named subtrees from the portal <c>resources.en.common</c> JSON object returned by
/// <see cref="BeDemo.Api.Services.LocalizationBundleService"/> for golden-file regression tests.
/// </summary>
/// <remarks>
/// <para>
/// Before static i18n migration, portal copy lived in <c>many_faces_portal/src/i18n/locales/en.json</c>
/// under the default namespace <c>common</c>. The API now serves the same shape as
/// <c>GET /api/localization/portal</c> → <c>resources.en.common</c>. Golden tests lock a representative
/// auth-flow subtree so accidental .resx edits cannot silently change login/register/route slugs.
/// </para>
/// <para>
/// Only explicit paths are compared — not the entire bundle — so unrelated keys can evolve without
/// updating the golden file on every copy change.
/// </para>
/// </remarks>
internal static class LocalizationGoldenSubtreeExtractor
{
	/// <summary>
	/// Relative JSON paths (dot-separated) under <paramref name="commonNamespace"/> included in golden exports.
	/// </summary>
	public static readonly string[] PortalAuthFlowPaths =
	[
		"pages.login",
		"pages.register",
		"routes.login",
		"routes.register",
		"routes.homepage",
	];

	private static readonly JsonSerializerOptions SerializeOptions = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>
	/// Builds a compact <see cref="JsonObject"/> containing only the configured subtrees.
	/// </summary>
	public static JsonObject ExtractPortalAuthFlow(JsonObject commonNamespace)
	{
		var golden = new JsonObject();
		foreach (var path in PortalAuthFlowPaths)
		{
			var node = TryGetByDotPath(commonNamespace, path);
			if (node == null)
				throw new InvalidOperationException($"Missing localization path '{path}' in portal common namespace.");

			SetByDotPath(golden, path, node.DeepClone());
		}

		return golden;
	}

	/// <summary>
	/// Serializes <paramref name="subtree"/> with stable indentation for golden file storage and diff-friendly failures.
	/// </summary>
	public static string ToCanonicalJson(JsonObject subtree) =>
		JsonSerializer.Serialize(subtree, SerializeOptions);

	/// <summary>
	/// Parses golden file text; throws when JSON is not an object.
	/// </summary>
	public static JsonObject ParseGoldenFile(string json)
	{
		var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Golden file is empty.");
		return node as JsonObject ?? throw new InvalidOperationException("Golden file root must be a JSON object.");
	}

	/// <summary>
	/// Deep-compares two JSON trees; returns a human-readable diff hint when they differ.
	/// </summary>
	public static bool DeepEquals(JsonNode? a, JsonNode? b, out string? difference)
	{
		difference = null;
		if (JsonNode.DeepEquals(a, b))
			return true;

		var aJson = a == null ? "null" : a.ToJsonString(SerializeOptions);
		var bJson = b == null ? "null" : b.ToJsonString(SerializeOptions);
		difference =
			"Golden subtree mismatch. Update Fixtures/portal-auth-flow-golden.en.json only when copy changes are intentional. "
			+ $"Expected:{Environment.NewLine}{aJson}{Environment.NewLine}Actual:{Environment.NewLine}{bJson}";
		return false;
	}

	private static JsonNode? TryGetByDotPath(JsonObject root, string dotPath)
	{
		JsonNode? current = root;
		foreach (var segment in dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out var next))
				return null;
			current = next;
		}

		return current;
	}

	private static void SetByDotPath(JsonObject root, string dotPath, JsonNode value)
	{
		var segments = dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var node = root;
		for (var i = 0; i < segments.Length; i++)
		{
			var segment = segments[i];
			var isLeaf = i == segments.Length - 1;
			if (isLeaf)
			{
				node[segment] = value;
				return;
			}

			if (node[segment] is not JsonObject child)
			{
				child = new JsonObject();
				node[segment] = child;
			}

			node = child;
		}
	}
}
