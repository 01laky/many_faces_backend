namespace BeDemo.Api.Services;

/// <summary>
/// Front-end and mobile URLs embedded in registration emails (<c>action_link</c> template param).
/// These are not API hosts — they point at portal routes or custom app deep links.
/// </summary>
public sealed class MailRegistrationLinkOptions
{
    public const string SectionName = "Mail:RegistrationLinks";

    /// <summary>Portal origin, e.g. <c>http://localhost:9081</c>.</summary>
    public string PortalPublicBaseUrl { get; set; } = "http://localhost:9081";

    /// <summary>Path template with <c>{locale}</c> segment, e.g. <c>/{locale}/register/complete</c>.</summary>
    public string CompleteRegistrationPathTemplate { get; set; } = "/{locale}/register/complete";

    /// <summary>Base for mobile deep links, e.g. <c>manyfaces://register/complete</c> (query <c>?hash=</c> appended in code).</summary>
    public string MobileDeepLinkBase { get; set; } = "manyfaces://register/complete";

    /// <summary>When true and request has <c>platform: mobile</c>, use <see cref="MobileDeepLinkBase"/> instead of portal URL.</summary>
    public bool PreferMobileDeepLinkWhenPlatformMobile { get; set; }
}
