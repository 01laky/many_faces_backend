namespace BeDemo.Api.Models.Requests.OAuth;

/// <summary>GET /api/oauth2/register/prefill?hash=</summary>
public sealed class RegisterPrefillQuery
{
    public string? Hash { get; set; }
}

/// <summary>GET /api/admin/registration-invites list query.</summary>
public sealed class AdminInviteListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? Status { get; set; }
    public string? EmailContains { get; set; }

    /// <summary>Deprecated — use <see cref="Page"/> / <see cref="PageSize"/>.</summary>
    public int? Skip { get; set; }

    /// <summary>Deprecated — use <see cref="Page"/> / <see cref="PageSize"/>.</summary>
    public int? Take { get; set; }
}

/// <summary>GET /api/localization/{app} bundle query.</summary>
public sealed class LocalizationBundleQuery
{
    public string? V { get; set; }
}
