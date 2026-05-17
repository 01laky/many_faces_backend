namespace BeDemo.Api.Configuration;

/// <summary>
/// SHV2 BE-U3 — HMAC-signed download URLs for files under <c>wwwroot/uploads/**</c>.
/// </summary>
public sealed class UploadsOptions
{
    public const string SectionName = "Uploads";

    /// <summary>
    /// Server secret for <see cref="Services.UploadSignedUrlService"/> (min 32 chars in production).
    /// Override via environment <c>Uploads__SigningSecret</c>.
    /// </summary>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>Default lifetime of <c>exp</c> query parameter on signed serve URLs.</summary>
    public int SignedUrlLifetimeMinutes { get; set; } = 60;
}
