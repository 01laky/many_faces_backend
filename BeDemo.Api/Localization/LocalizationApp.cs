namespace BeDemo.Api.Localization;

/// <summary>Client applications that load static UI bundles from <c>GET /api/localization/{app}</c>.</summary>
public enum LocalizationApp
{
    Portal,
    Admin,
    Mobile,
}

public static class LocalizationAppParser
{
    public static bool TryParse(string? app, out LocalizationApp parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(app))
            return false;

        return app.Trim().ToLowerInvariant() switch
        {
            "portal" => Assign(LocalizationApp.Portal, out parsed),
            "admin" => Assign(LocalizationApp.Admin, out parsed),
            "mobile" => Assign(LocalizationApp.Mobile, out parsed),
            _ => false,
        };
    }

    private static bool Assign(LocalizationApp value, out LocalizationApp parsed)
    {
        parsed = value;
        return true;
    }
}
