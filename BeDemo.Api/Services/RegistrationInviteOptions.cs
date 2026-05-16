namespace BeDemo.Api.Services;

/// <summary>
/// Bound from <c>RegistrationInvite:*</c> in appsettings (see also mail link options under <see cref="MailRegistrationLinkOptions"/>).
/// </summary>
public sealed class RegistrationInviteOptions
{
    public const string SectionName = "RegistrationInvite";

    /// <summary>Length of the human verification code emailed to the user (e.g. 6).</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Minutes until <see cref="Models.RegistrationInvite.ExpiresAtUtc"/>; after that complete/prefill treat invite as inactive.</summary>
    public int ExpiryMinutes { get; set; } = 30;

    /// <summary>Max wrong <c>code</c> submissions per invite before complete is rejected.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Secret mixed into HMAC for <see cref="RegistrationInviteCrypto.HashCode"/>; must be consistent across all API instances.</summary>
    public string HmacPepper { get; set; } = "dev-registration-invite-pepper-change-me";

    /// <summary>How often <see cref="RegistrationInviteCleanupHostedService"/> runs.</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>Consumed invites are kept this many days for support, then deleted by cleanup.</summary>
    public int ConsumedRetentionDays { get; set; } = 7;
}
