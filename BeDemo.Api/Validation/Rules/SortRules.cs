namespace BeDemo.Api.Validation.Rules;

/// <summary>Shared sort query validation helpers (admin table server filter/sort rollout).</summary>
public static class SortRules
{
    /// <summary>Reject dynamic LINQ / injection patterns in sortBy tokens.</summary>
    public static bool IsSafeSortByToken(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        foreach (var c in sortBy)
        {
            if (c is '.' or '(' or ')' or '\0' or ' ' or '\t' or '\r' or '\n')
                return false;
        }

        return true;
    }

    public static bool IsValidSortDirection(string? sortDir) =>
        string.IsNullOrWhiteSpace(sortDir) ||
        sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
        sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Case-insensitive whitelist match; empty sortBy is allowed (default order).</summary>
    public static bool IsWhitelistedSortBy(string? sortBy, IReadOnlyCollection<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        if (!IsSafeSortByToken(sortBy))
            return false;

        return allowed.Any(a => a.Equals(sortBy, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDescending(string? sortDir) =>
        sortDir != null && sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);
}
