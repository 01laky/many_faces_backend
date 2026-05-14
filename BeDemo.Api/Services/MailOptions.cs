namespace BeDemo.Api.Services;

/// <summary>
/// Optional many_faces_mailer worker: the API sends transactional templates over gRPC; rendering and SMTP live in Java.
/// When <see cref="Enabled"/> is false, <see cref="IEmailSender"/> no-ops so Identity flows never throw in dev stacks without Mailpit.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>Master switch; keep false until <see cref="WorkerGrpcUrl"/> points at a reachable worker.</summary>
    public bool Enabled { get; set; }

    /// <summary>Absolute gRPC address, e.g. <c>http://mailer-worker:50054</c> on <c>many_faces_main_dev-network</c>.</summary>
    public string? WorkerGrpcUrl { get; set; }

    public string? WorkerTlsServerCaPath { get; set; }
    public string? WorkerTlsClientCertPath { get; set; }
    public string? WorkerTlsClientKeyPath { get; set; }
    public string? WorkerGrpcTlsServerName { get; set; }

    /// <summary>Shared secret as metadata <c>x-mailer-worker-token</c>; must match <c>MAILER_WORKER_EXPECTED_TOKEN</c> on the worker.</summary>
    public string? WorkerAuthToken { get; set; }

    public int GrpcDeadlineSeconds { get; set; } = 30;

    /// <summary>BCP 47 fallback when HTTP culture is not available (background jobs, tests).</summary>
    public string DefaultLocale { get; set; } = "en";

    public bool IsWorkerAddressValid =>
        !string.IsNullOrWhiteSpace(WorkerGrpcUrl) &&
        Uri.TryCreate(WorkerGrpcUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public bool IsEnabled => Enabled && IsWorkerAddressValid;
}
