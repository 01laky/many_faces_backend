using BeDemo.Api.Models;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Utils;

/// <summary>EF-safe sort application for admin list endpoints (whitelist validated upstream).</summary>
public static class ListSortApplicators
{
    /// <summary>Maps validated <c>sortBy</c> to EF <c>OrderBy</c>; default newest first when sort omitted.</summary>
    public static IOrderedQueryable<ApplicationUser> ApplyUsersSort(
        IQueryable<ApplicationUser> query,
        string? sortBy,
        string? sortDir)
    {
        var desc = SortRules.IsDescending(sortDir);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "id" => desc ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id),
            "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "firstname" => desc
                ? query.OrderByDescending(u => u.FirstName ?? string.Empty)
                : query.OrderBy(u => u.FirstName ?? string.Empty),
            "lastname" => desc
                ? query.OrderByDescending(u => u.LastName ?? string.Empty)
                : query.OrderBy(u => u.LastName ?? string.Empty),
            "createdat" => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt),
        };
    }

    public static IOrderedQueryable<Face> ApplyFacesSort(IQueryable<Face> query, string? sortBy, string? sortDir)
    {
        var desc = SortRules.IsDescending(sortDir);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "id" => desc ? query.OrderByDescending(f => f.Id) : query.OrderBy(f => f.Id),
            "index" => desc ? query.OrderByDescending(f => f.Index) : query.OrderBy(f => f.Index),
            "title" => desc ? query.OrderByDescending(f => f.Title) : query.OrderBy(f => f.Title),
            "ispublic" => desc ? query.OrderByDescending(f => f.IsPublic) : query.OrderBy(f => f.IsPublic),
            "createdat" => desc ? query.OrderByDescending(f => f.CreatedAt) : query.OrderBy(f => f.CreatedAt),
            "updatedat" => desc
                ? query.OrderByDescending(f => f.UpdatedAt ?? DateTime.MinValue)
                : query.OrderBy(f => f.UpdatedAt ?? DateTime.MinValue),
            _ => query.OrderBy(f => f.Index),
        };
    }

    /// <summary>CMS pages per face; default stable order by index then name.</summary>
    public static IOrderedQueryable<Page> ApplyPagesSort(IQueryable<Page> query, string? sortBy, string? sortDir)
    {
        var desc = SortRules.IsDescending(sortDir);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "id" => desc ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
            "name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "path" => desc ? query.OrderByDescending(p => p.Path) : query.OrderBy(p => p.Path),
            "index" => desc ? query.OrderByDescending(p => p.Index) : query.OrderBy(p => p.Index),
            "createdat" => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            "updatedat" => desc
                ? query.OrderByDescending(p => p.UpdatedAt ?? DateTime.MinValue)
                : query.OrderBy(p => p.UpdatedAt ?? DateTime.MinValue),
            _ => query.OrderBy(p => p.Index).ThenBy(p => p.Name),
        };
    }
}
