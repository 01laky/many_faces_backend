namespace BeDemo.Api.Services;

/// <summary>Env/bootstrap SMTP relay defaults merged into <see cref="OperatorMail.OperatorMailSettingsService"/> on first read.</summary>
public sealed class MailSmtpBootstrapOptions
{
	public string Host { get; set; } = "mailpit";

	public int Port { get; set; } = 1025;

	public bool StartTls { get; set; }

	public string? User { get; set; }

	public string? Password { get; set; }
}

/// <summary>Env/bootstrap From identity for mail.</summary>
public sealed class MailFromBootstrapOptions
{
	public string Email { get; set; } = "no-reply@example.invalid";

	public string DisplayName { get; set; } = "Many Faces";
}
