namespace BeDemo.Api.Services;

/// <summary>
/// Optional push worker configuration: many_faces_backend calls the Go <c>many_faces_push</c> process over gRPC for FCM dispatch.
/// When <see cref="Enabled"/> is false or <see cref="WorkerGrpcUrl"/> is unset, DI registers a no-op client and product code must not throw.
/// </summary>
public sealed class PushOptions
{
	public const string SectionName = "Push";

	/// <summary>Master switch for calling the push worker; keep false where Firebase credentials are not mounted.</summary>
	public bool Enabled { get; set; }

	/// <summary>Absolute gRPC address, e.g. <c>http://push-worker-dev:50053</c> on <c>many_faces_main_dev-network</c>.</summary>
	public string? WorkerGrpcUrl { get; set; }

	/// <summary>Optional PEM for private CA when <see cref="WorkerGrpcUrl"/> uses <c>https://</c>.</summary>
	public string? WorkerTlsServerCaPath { get; set; }

	/// <summary>Optional mTLS client cert PEM when the worker requires client certificates.</summary>
	public string? WorkerTlsClientCertPath { get; set; }

	/// <summary>Optional mTLS client key PEM matching <see cref="WorkerTlsClientCertPath"/>.</summary>
	public string? WorkerTlsClientKeyPath { get; set; }

	/// <summary>Optional TLS hostname override for certificate validation.</summary>
	public string? WorkerGrpcTlsServerName { get; set; }

	/// <summary>Shared secret sent as metadata <c>x-push-worker-token</c>; must match <c>PUSH_WORKER_EXPECTED_TOKEN</c> on the worker.</summary>
	public string? WorkerAuthToken { get; set; }

	/// <summary>Per-RPC deadline in seconds (clamped by the client implementation).</summary>
	public int GrpcDeadlineSeconds { get; set; } = 15;

	/// <summary>Default title localization key for smoke / pilot sends.</summary>
	public string DefaultTitleLocKey { get; set; } = "push_test_title";

	/// <summary>Default body localization key for smoke / pilot sends.</summary>
	public string DefaultBodyLocKey { get; set; } = "push_test_body";

	/// <summary>Optional Android notification channel id for smoke sends.</summary>
	public string? DefaultAndroidChannelId { get; set; }

	/// <summary>Firebase bootstrap options (nested <c>Push:Firebase</c>).</summary>
	public PushFirebaseBootstrapOptions Firebase { get; set; } = new();

	/// <summary>True when the worker URL is usable for gRPC channel construction.</summary>
	public bool IsWorkerAddressValid =>
		!string.IsNullOrWhiteSpace(WorkerGrpcUrl) &&
		Uri.TryCreate(WorkerGrpcUrl, UriKind.Absolute, out var uri) &&
		(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

	/// <summary>True when push worker gRPC should be opened at startup.</summary>
	public bool IsEnabled => Enabled && IsWorkerAddressValid;
}
