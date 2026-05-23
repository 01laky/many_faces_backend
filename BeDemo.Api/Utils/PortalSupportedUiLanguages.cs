namespace BeDemo.Api.Utils;

/// <summary>Portal static UI language codes (keep aligned with many_faces_portal src/i18n/constants.ts).</summary>
public static class PortalSupportedUiLanguages
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "sk", "cz", "de", "fr", "it",
    };

    public static bool IsAllowed(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Allowed.Contains(code.Trim());
}
