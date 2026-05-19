namespace BeDemo.Api.Models.Requests.Pages;

public sealed class GetPagesQuery
{
    public int? FaceId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public sealed class UpsertPageTranslationsRequest
{
    public List<PageTranslationItem> Translations { get; set; } = [];
}

public sealed class PageTranslationItem
{
    public string LanguageCode { get; set; } = string.Empty;
    public string TranslatedRoute { get; set; } = string.Empty;
}
