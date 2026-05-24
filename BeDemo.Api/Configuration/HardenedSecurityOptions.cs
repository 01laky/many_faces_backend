namespace BeDemo.Api.Configuration;

/// <summary>
/// Flags for <see cref="HardenedSecurityValidateOptions"/> when <c>ASPNETCORE_ENVIRONMENT=Hardened</c>.
/// Bound from <c>HardenedSecurity:*</c> in <c>appsettings.Hardened.json</c>.
/// </summary>
public sealed class HardenedSecurityOptions
{
    public const string SectionName = "HardenedSecurity";

    /// <summary>Require <c>https://</c> worker URLs and non-empty bearer tokens when workers are enabled.</summary>
    public bool EnforceWorkerTlsAndTokens { get; set; } = true;

    /// <summary>Require stable <c>Jwt:SigningPemPath</c> (no ephemeral prod keys).</summary>
    public bool EnforceJwtSigningPem { get; set; } = true;

    /// <summary>Reject placeholder upload / invite pepper / OAuth secrets in configuration.</summary>
    public bool RejectPlaceholderSecrets { get; set; } = true;
}
