using System.Text.Json.Nodes;

namespace BeDemo.Api.Localization;

/// <summary>
/// Converts flat dotted resource keys into nested JSON objects for i18next.
/// </summary>
/// <remarks>
/// Flat keys must not use one entry as both a leaf and a branch prefix (e.g. <c>pages.login</c> and
/// <c>pages.login.title</c>). <see cref="FindAmbiguousFlatKeys"/> detects that at build/CI time;
/// <see cref="ToNestedObject"/> throws at runtime if ambiguous pairs slip through.
/// </remarks>
public static class ResourceJsonUnflattener
{
    /// <summary>
    /// Finds pairs of flat keys where <paramref name="parent"/> is a strict dotted prefix of <paramref name="child"/>,
    /// which would make <see cref="ToNestedObject"/> fail or produce inconsistent trees.
    /// </summary>
    /// <param name="keys">Dotted resource names from <c>.resx</c> (e.g. <c>pages.login.title</c>).</param>
    /// <returns>Ordered list of conflicting (<c>parent</c>, <c>child</c>) pairs; empty when the set is safe.</returns>
    public static IReadOnlyList<(string Parent, string Child)> FindAmbiguousFlatKeys(IEnumerable<string> keys)
    {
        var sorted = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var conflicts = new List<(string Parent, string Child)>();
        for (var i = 0; i < sorted.Count; i++)
        {
            var parent = sorted[i];
            var prefix = parent + ".";
            for (var j = i + 1; j < sorted.Count; j++)
            {
                var child = sorted[j];
                if (!child.StartsWith(prefix, StringComparison.Ordinal))
                    break;

                conflicts.Add((parent, child));
            }
        }

        return conflicts;
    }

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
