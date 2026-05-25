using ManyFaces.Search.V1;

namespace BeDemo.Api.Services.Search;

/// <summary>Normalizes worker route id keys to admin SPA detail path contract.</summary>
public static class SearchRouteParamsNormalizer
{
    private static readonly Dictionary<string, string> LegacyIdKeyByType = new(StringComparer.Ordinal)
    {
        [SearchDocumentTypes.Page] = "pageId",
        [SearchDocumentTypes.Album] = "albumId",
        [SearchDocumentTypes.Blog] = "blogId",
        [SearchDocumentTypes.Reel] = "reelId",
        [SearchDocumentTypes.Story] = "storyId",
    };

    public static Dictionary<string, string> Normalize(string documentType, string entityId, RouteParams? routeParams)
    {
        var ids = routeParams?.Ids is { Count: > 0 }
            ? routeParams.Ids.ToDictionary(kv => kv.Key, kv => kv.Value)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        if (LegacyIdKeyByType.TryGetValue(documentType, out var typedKey))
        {
            if (!ids.ContainsKey(typedKey))
            {
                if (ids.TryGetValue("id", out var legacyId))
                    ids[typedKey] = legacyId;
                else if (!string.IsNullOrWhiteSpace(entityId))
                    ids[typedKey] = entityId;
            }

            ids.Remove("id");
        }

        if (documentType == SearchDocumentTypes.FaceProfile
            && !ids.ContainsKey("userId")
            && ids.TryGetValue("profileId", out var profileOnly))
        {
            ids["profileId"] = profileOnly;
        }

        return ids;
    }
}
