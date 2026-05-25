namespace BeDemo.Api.Models.DTOs.Search;

/// <summary>Admin global search autocomplete response (§3.2).</summary>
public sealed class AdminSearchAutocompleteResponse
{
    public string Query { get; set; } = string.Empty;

    public int Offset { get; set; }

    public int PageSize { get; set; }

    public IReadOnlyList<AdminSearchAutocompleteHitDto> Hits { get; set; } = [];

    public bool HasMore { get; set; }

    public int NextOffset { get; set; }

    public bool SearchAvailable { get; set; } = true;

    public string? Message { get; set; }
}

public sealed class AdminSearchAutocompleteHitDto
{
    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? FaceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public IReadOnlyList<string> Highlights { get; set; } = [];

    public double Score { get; set; }

    public AdminSearchRouteParamsDto RouteParams { get; set; } = new();
}

public sealed class AdminSearchRouteParamsDto
{
    public string Type { get; set; } = string.Empty;

    public Dictionary<string, string> Ids { get; set; } = new();
}
