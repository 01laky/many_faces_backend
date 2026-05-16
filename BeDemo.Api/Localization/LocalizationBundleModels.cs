using System.Text.Json.Nodes;

namespace BeDemo.Api.Localization;

public sealed class LocalizationBundleResponse
{
    public required string App { get; init; }
    public required string Version { get; init; }
    public required string DefaultNamespace { get; init; }
    public required IReadOnlyList<string> SupportedLanguages { get; init; }
    public required Dictionary<string, Dictionary<string, JsonObject>> Resources { get; init; }
}
