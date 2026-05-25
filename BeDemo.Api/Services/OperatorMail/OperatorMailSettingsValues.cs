namespace BeDemo.Api.Services.OperatorMail;

/// <summary>Resolved mail runtime values (DB merged over env bootstrap). Secrets exist in-memory only for gRPC sends.</summary>
public sealed record OperatorMailSettingsValues(
	bool Enabled,
	string DefaultLocale,
	string? WorkerGrpcUrl,
	string? WorkerAuthTokenPlaintext,
	string SmtpHost,
	int SmtpPort,
	bool SmtpStartTls,
	string? SmtpUser,
	string? SmtpPasswordPlaintext,
	string FromEmail,
	string? FromDisplayName,
	string PortalPublicBaseUrl,
	string CompleteRegistrationPathTemplate,
	string MobileDeepLinkBase,
	bool PreferMobileDeepLinkWhenPlatformMobile,
	DateTime UpdatedAtUtc,
	string? UpdatedByUserId)
{
	public bool HasWorkerAuthToken => !string.IsNullOrWhiteSpace(WorkerAuthTokenPlaintext);

	public bool HasSmtpPassword => !string.IsNullOrWhiteSpace(SmtpPasswordPlaintext);

	public bool IsWorkerAddressValid =>
		!string.IsNullOrWhiteSpace(WorkerGrpcUrl) &&
		Uri.TryCreate(WorkerGrpcUrl, UriKind.Absolute, out var uri) &&
		(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

	public bool IsSmtpComplete =>
		!string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(FromEmail);

	public bool IsSendAllowed => Enabled && IsWorkerAddressValid && IsSmtpComplete;

	public string EffectiveStatus =>
		!Enabled ? OperatorMailEffectiveStatuses.Disabled :
		!IsSmtpComplete || !IsWorkerAddressValid ? OperatorMailEffectiveStatuses.Incomplete :
		OperatorMailEffectiveStatuses.Configured;

	public string ChannelCacheKey =>
		$"{WorkerGrpcUrl?.Trim()}|{WorkerAuthTokenPlaintext?.Length ?? 0}";
}

public static class OperatorMailEffectiveStatuses
{
	public const string Disabled = "disabled";
	public const string Incomplete = "incomplete";
	public const string Configured = "configured";
	public const string Unreachable = "unreachable";
	public const string Degraded = "degraded";
}
