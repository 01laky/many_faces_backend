namespace BeDemo.Api.Configuration;

/// <summary>
/// JWT access-token lifetimes for OAuth2 password grant and refresh rotation (SHV2 <b>BE-A2</b>).
/// </summary>
/// <remarks>
/// <para>
/// <b>rememberMe</b> selects <see cref="ExpiresInMinutesRememberMe"/> (longer access JWT). Opaque refresh rows use
/// <see cref="RefreshTokenDaysSession"/> / <see cref="RefreshTokenDaysRememberMe"/> independently — a user can hold a
/// 7-day access token while refresh remains valid up to 90 days when "stay signed in" is enabled.
/// </para>
/// <para>
/// Historical misconfiguration used multi-year <c>ExpiresInMinutesRememberMe</c> (e.g. 5_256_000 minutes).
/// <see cref="MaxRememberMeAccessMinutes"/> enforces a hard ceiling at startup via <c>ValidateOnStart</c>.
/// </para>
/// </remarks>
public sealed class JwtTokenLifetimeOptions
{
    /// <summary>Configuration section name (<c>appsettings.json</c> → <c>Jwt</c>).</summary>
    public const string SectionName = "Jwt";

    /// <summary>SHV2 BE-A2 recommended access-token lifetime when <c>rememberMe</c> is true: seven days.</summary>
    public const int RecommendedRememberMeAccessMinutes = 7 * 24 * 60;

    /// <summary>Maximum allowed <see cref="ExpiresInMinutesRememberMe"/> (same as recommended policy).</summary>
    public const int MaxRememberMeAccessMinutes = RecommendedRememberMeAccessMinutes;

    /// <summary>Legacy value observed in repo before BE-A2 (≈10 years) — used only in validation error messages.</summary>
    public const int LegacyMisconfiguredRememberMeMinutes = 5_256_000;

    /// <summary>Default access-token lifetime when <c>rememberMe</c> is false, omitted, or null.</summary>
    public int ExpiresInMinutes { get; set; } = 60;

    /// <summary>Access-token lifetime when <c>rememberMe</c> is true (must be ≤ <see cref="MaxRememberMeAccessMinutes"/>).</summary>
    public int ExpiresInMinutesRememberMe { get; set; } = RecommendedRememberMeAccessMinutes;

    /// <summary>Opaque refresh-token absolute lifetime (days) for normal session logins.</summary>
    public int RefreshTokenDaysSession { get; set; } = 14;

    /// <summary>Opaque refresh-token absolute lifetime (days) when access JWT used remember-me minutes.</summary>
    public int RefreshTokenDaysRememberMe { get; set; } = 90;

    /// <summary>Resolves configured access-token minutes for the password-grant <c>rememberMe</c> flag.</summary>
    public int ResolveAccessTokenMinutes(bool useRememberMeAccessLifetime) =>
        useRememberMeAccessLifetime ? ExpiresInMinutesRememberMe : ExpiresInMinutes;
}
