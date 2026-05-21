using System.Text.Json;

namespace BeDemo.Api.Validation.Pages;

public static class ProfileDetailGridSchemaValidator
{
    private const int SupportedSchemaVersion = 1;
    private const int MaxItems = 40;
    private const int MaxJsonLength = 64 * 1024;

    private static readonly HashSet<string> AllowedSectionTypes = new(StringComparer.Ordinal)
    {
        "profileHero",
        "profileMeta",
        "profileActions",
        "profileComments",
        "profileReviews",
        "profileBackNav",
        "userAlbums",
        "userBlogs",
        "userReels",
        "userStories",
        "spacer",
    };

    public static string? Validate(string? gridSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(gridSchemaJson))
            return "gridSchema is required for profile detail template pages";

        if (gridSchemaJson.Length > MaxJsonLength)
            return "gridSchema exceeds maximum allowed size";

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(gridSchemaJson);
        }
        catch (JsonException)
        {
            return "gridSchema must be valid JSON";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "gridSchema root must be an object";

            var root = doc.RootElement;

            if (root.TryGetProperty("schemaVersion", out var versionEl))
            {
                // The portal currently knows schema v1 only. Treat any non-integer or future
                // version as unsupported so malformed admin input returns 400 instead of a parser exception.
                if (versionEl.ValueKind != JsonValueKind.Number ||
                    !versionEl.TryGetInt32(out var schemaVersion) ||
                    schemaVersion > SupportedSchemaVersion)
                {
                    return "Unsupported schemaVersion";
                }
            }

            if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                return "gridSchema.items must be an array";

            if (itemsEl.GetArrayLength() > MaxItems)
                return $"gridSchema.items exceeds maximum of {MaxItems}";

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in itemsEl.EnumerateArray())
            {
                // Every grid entry must be addressable by react-grid-layout (i) and mapped to
                // one of the backend-approved profile detail sections. Layout coordinates stay
                // portal-owned, so the backend only validates identity and section allow-listing.
                if (item.ValueKind != JsonValueKind.Object)
                    return "Each grid item must be an object";

                if (!item.TryGetProperty("i", out var idEl) || idEl.ValueKind != JsonValueKind.String)
                    return "Each grid item requires string property i";

                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id))
                    return "Grid item i must be non-empty";

                if (!ids.Add(id!))
                    return $"Duplicate grid item id: {id}";

                if (!item.TryGetProperty("sectionType", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                    return $"Grid item {id} requires sectionType";

                var sectionType = typeEl.GetString();
                if (string.IsNullOrWhiteSpace(sectionType) || !AllowedSectionTypes.Contains(sectionType))
                    return $"Unknown or missing sectionType on item {id}";
            }
        }

        return null;
    }
}
