namespace BeDemo.Api.Services.OperatorPush;

/// <summary>Resolved push runtime values (DB merged over env bootstrap). Secrets exist in-memory only for gRPC sends.</summary>
public sealed record OperatorPushSettingsValues(
	bool Enabled,
	string? WorkerGrpcUrl,
	string? WorkerAuthTokenPlaintext,
	string? FirebaseProjectId,
	string? FirebaseServiceAccountJsonPlaintext,
	string DefaultTitleLocKey,
	string DefaultBodyLocKey,
	string? DefaultAndroidChannelId,
	int GrpcDeadlineSeconds,
	DateTime UpdatedAtUtc,
	string? UpdatedByUserId)
{
	public bool HasWorkerAuthToken => !string.IsNullOrWhiteSpace(WorkerAuthTokenPlaintext);

	public bool HasFirebaseCredentials => !string.IsNullOrWhiteSpace(FirebaseServiceAccountJsonPlaintext);

	public bool IsWorkerAddressValid =>
		!string.IsNullOrWhiteSpace(WorkerGrpcUrl) &&
		Uri.TryCreate(WorkerGrpcUrl, UriKind.Absolute, out var uri) &&
		(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

	public bool IsPushComplete =>
		IsWorkerAddressValid &&
		HasFirebaseCredentials &&
		!string.IsNullOrWhiteSpace(DefaultTitleLocKey) &&
		!string.IsNullOrWhiteSpace(DefaultBodyLocKey);

	public bool IsSendAllowed => Enabled && IsPushComplete;

	public string EffectiveStatus =>
		!Enabled ? OperatorPushEffectiveStatuses.Disabled :
		!IsPushComplete ? OperatorPushEffectiveStatuses.Incomplete :
		OperatorPushEffectiveStatuses.Configured;

	public string ChannelCacheKey =>
		$"{WorkerGrpcUrl?.Trim()}|{WorkerAuthTokenPlaintext?.Length ?? 0}|{GrpcDeadlineSeconds}";
}

public static class OperatorPushEffectiveStatuses
{
	public const string Disabled = "disabled";
	public const string Incomplete = "incomplete";
	public const string Configured = "configured";
	public const string Unreachable = "unreachable";
	public const string Degraded = "degraded";
}
