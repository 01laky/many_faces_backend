namespace BeDemo.Api.Models;

/// <summary>
/// Singleton platform row: operator-editable mail settings (platform + SMTP) for admin Settings.
/// Secrets are stored encrypted via <see cref="Services.OperatorMail.IOperatorMailSecretProtector"/>.
/// </summary>
public class OperatorMailSystemSettings
{
	/// <summary>Always <c>1</c> — platform-wide singleton.</summary>
	public int Id { get; set; } = 1;

	public bool Enabled { get; set; }

	public string DefaultLocale { get; set; } = "en";

	public string? WorkerGrpcUrl { get; set; }

	public string? WorkerAuthTokenCiphertext { get; set; }

	public string SmtpHost { get; set; } = string.Empty;

	public int SmtpPort { get; set; } = 1025;

	public bool SmtpStartTls { get; set; }

	public string? SmtpUser { get; set; }

	public string? SmtpPasswordCiphertext { get; set; }

	public string FromEmail { get; set; } = string.Empty;

	public string? FromDisplayName { get; set; }

	public string PortalPublicBaseUrl { get; set; } = "http://localhost:9081";

	public string CompleteRegistrationPathTemplate { get; set; } = "/{locale}/register/complete";

	public string MobileDeepLinkBase { get; set; } = "manyfaces://register/complete";

	public bool PreferMobileDeepLinkWhenPlatformMobile { get; set; }

	public DateTime UpdatedAtUtc { get; set; }

	public string? UpdatedByUserId { get; set; }
}
