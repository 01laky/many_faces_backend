using System.Text.Json.Nodes;

namespace BeDemo.Api.Localization;

/// <summary>
/// Converts flat dotted resource keys into nested JSON objects for i18next.
/// </summary>
public static class ResourceJsonUnflattener
{
    /// <summary>
    /// Builds a nested <see cref="JsonObject"/> from flat key/value pairs.
    /// </summary>
    public static JsonObject ToNestedObject(IReadOnlyDictionary<string, string> flatEntries)
    {
        var root = new JsonObject();
        foreach (var (key, value) in flatEntries)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
                continue;

            var node = root;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLeaf = i == segments.Length - 1;
                if (isLeaf)
                {
                    if (node[segment] is JsonObject)
                        throw new InvalidOperationException($"Ambiguous localization key '{key}': branch exists at leaf.");
                    node[segment] = value;
                }
                else
                {
                    if (node[segment] is JsonValue)
                        throw new InvalidOperationException($"Ambiguous localization key '{key}': leaf exists at branch.");
                    if (node[segment] is not JsonObject child)
                    {
                        child = new JsonObject();
                        node[segment] = child;
                    }
                    node = child;
                }
            }
        }

        return root;
    }

    /// <summary>
    /// Splits mobile flat keys (<c>common.foo</c>) into per-namespace nested objects.
    /// </summary>
    public static Dictionary<string, JsonObject> ToMobileNamespaces(IReadOnlyDictionary<string, string> flatEntries)
    {
        var byNs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in flatEntries)
        {
            var dot = key.IndexOf('.');
            if (dot <= 0 || dot >= key.Length - 1)
                continue;
            var ns = key[..dot];
            var rest = key[(dot + 1)..];
            if (!byNs.TryGetValue(ns, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.Ordinal);
                byNs[ns] = dict;
            }
            dict[rest] = value;
        }

        return byNs.ToDictionary(
            kv => kv.Key,
            kv => ToNestedObject(kv.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}
