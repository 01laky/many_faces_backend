namespace BeDemo.Api.Services.Search;

/// <summary>Canonical <c>document_type</c> values for admin global search (§1.2 entity catalog).</summary>
public static class SearchDocumentTypes
{
    public const string User = "user";
    public const string Face = "face";
    public const string Page = "page";
    public const string Album = "album";
    public const string Blog = "blog";
    public const string Reel = "reel";
    public const string Story = "story";
    public const string FaceChatRoom = "face_chat_room";
    public const string VideoLounge = "video_lounge";
    public const string FaceProfile = "face_profile";
    public const string WallTicket = "wall_ticket";

    /// <summary>Stable iteration order for reconciliation and mixed-type sort (§7.2 GSH1-T-A11).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        User,
        Face,
        Page,
        Album,
        Blog,
        Reel,
        Story,
        FaceChatRoom,
        VideoLounge,
        FaceProfile,
        WallTicket,
    ];

    private static readonly Dictionary<string, int> SortOrderMap =
        All.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

    /// <summary>Lower values sort first when scores tie.</summary>
    public static int SortOrder(string documentType) =>
        SortOrderMap.TryGetValue(documentType, out var order) ? order : int.MaxValue;
}
