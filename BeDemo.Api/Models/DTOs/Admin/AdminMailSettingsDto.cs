namespace BeDemo.Api.Models.DTOs.Admin;

public sealed class AdminMailSettingsDto
{
	public bool Enabled { get; init; }

	public required string DefaultLocale { get; init; }

	public string? WorkerGrpcUrl { get; init; }

	public bool HasWorkerAuthToken { get; init; }

	public required AdminMailSmtpSettingsDto Smtp { get; init; }

	public required AdminMailFromSettingsDto From { get; init; }

	public required AdminMailRegistrationLinksDto RegistrationLinks { get; init; }

	public required string EffectiveStatus { get; init; }

	public DateTime UpdatedAtUtc { get; init; }

	public string? UpdatedByUserId { get; init; }
}

public sealed class AdminMailSmtpSettingsDto
{
	public required string Host { get; init; }

	public int Port { get; init; }

	public bool StartTls { get; init; }

	public string? User { get; init; }

	public bool HasPassword { get; init; }
}

public sealed class AdminMailFromSettingsDto
{
	public required string Email { get; init; }

	public string? DisplayName { get; init; }
}

public sealed class AdminMailRegistrationLinksDto
{
	public required string PortalPublicBaseUrl { get; init; }

	public required string CompleteRegistrationPathTemplate { get; init; }

	public required string MobileDeepLinkBase { get; init; }

	public bool PreferMobileDeepLinkWhenPlatformMobile { get; init; }
}

public sealed class AdminMailTestSmtpResultDto
{
	public bool SmtpReachable { get; init; }

	public string? Message { get; init; }
}
