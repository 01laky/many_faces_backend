namespace BeDemo.Api.Models.Requests.Admin;

public sealed class UpdateAdminMailSettingsRequest
{
	public bool Enabled { get; set; }

	public string? DefaultLocale { get; set; }

	public string? WorkerGrpcUrl { get; set; }

	/// <summary>Omit to keep; empty string clears.</summary>
	public string? WorkerAuthToken { get; set; }

	public UpdateAdminMailSmtpRequest? Smtp { get; set; }

	public UpdateAdminMailFromRequest? From { get; set; }

	public UpdateAdminMailRegistrationLinksRequest? RegistrationLinks { get; set; }
}

public sealed class UpdateAdminMailSmtpRequest
{
	public string? Host { get; set; }

	public int? Port { get; set; }

	public bool? StartTls { get; set; }

	public string? User { get; set; }

	/// <summary>Omit to keep; empty string clears.</summary>
	public string? Password { get; set; }
}

public sealed class UpdateAdminMailFromRequest
{
	public string? Email { get; set; }

	public string? DisplayName { get; set; }
}

public sealed class UpdateAdminMailRegistrationLinksRequest
{
	public string? PortalPublicBaseUrl { get; set; }

	public string? CompleteRegistrationPathTemplate { get; set; }

	public string? MobileDeepLinkBase { get; set; }

	public bool? PreferMobileDeepLinkWhenPlatformMobile { get; set; }
}
