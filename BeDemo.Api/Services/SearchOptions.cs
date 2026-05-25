namespace BeDemo.Api.Services;

/// <summary>
/// Optional search tier configuration: the API talks to the Go <strong>search-worker</strong> in <c>many_faces_elastic</c> over gRPC only.
/// When <see cref="Enabled"/> is false or <see cref="WorkerGrpcUrl"/> is unset/invalid, search endpoints stay off and default <c>dotnet test</c> does not need Docker infra.
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Master switch for the search feature slice; keep false in CI and laptops without many_faces_elastic.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Absolute gRPC server address for the worker, e.g. <c>http://search-worker-dev:50052</c> on <c>many_faces_main_dev-network</c>.
    /// Plaintext HTTP/2 (h2c) is acceptable only on trusted dev Docker networks; production should use <c>https://</c> plus the TLS options below (see <c>docs/guides/elasticsearch-grpc-tls-mtls.md</c>).
    /// </summary>
    public string? WorkerGrpcUrl { get; set; }

    /// <summary>
    /// When <see cref="WorkerGrpcUrl"/> uses <c>https://</c>, optional PEM file path for extra CA certificates (private CA / self-signed server). When empty, the platform default trust store is used.
    /// </summary>
    public string? WorkerTlsServerCaPath { get; set; }

    /// <summary>
    /// When the worker is configured for mTLS, PEM paths for the client (service) certificate and private key presented to the worker (must chain to the worker's <c>SEARCH_WORKER_GRPC_MTLS_CLIENT_CA_FILE</c> bundle).
    /// </summary>
    public string? WorkerTlsClientCertPath { get; set; }

    /// <summary>PEM private key matching <see cref="WorkerTlsClientCertPath"/>.</summary>
    public string? WorkerTlsClientKeyPath { get; set; }

    /// <summary>
    /// Optional TLS hostname (SNI / certificate validation) when it differs from the host in <see cref="WorkerGrpcUrl"/> (e.g. URL uses an IP or short Docker name but the cert is issued for a longer DNS name).
    /// </summary>
    public string? WorkerGrpcTlsServerName { get; set; }

    /// <summary>
    /// Optional shared secret sent as gRPC metadata <c>x-search-worker-token</c>; must match <c>SEARCH_WORKER_EXPECTED_TOKEN</c> in the worker container when that env is set.
    /// </summary>
    public string? WorkerAuthToken { get; set; }

    /// <summary>Per-RPC deadline for Ping and future search/index calls.</summary>
    public int GrpcDeadlineSeconds { get; set; } = 10;

    /// <summary>Prefix for future Elasticsearch index names (reserved for indexing phases).</summary>
    public string IndexPrefix { get; set; } = "manyfaces";

    /// <summary>Reserved for hosted Elastic / API key auth inside the worker only (not used by the API HTTP path).</summary>
    public string? ApiKey { get; set; }

    /// <summary>When false, <see cref="SearchIndexReconciliationHostedService"/> is not registered.</summary>
    public bool ReconciliationEnabled { get; set; } = true;

    /// <summary>Hours between full reconciliation runs after the first startup run completes.</summary>
    public int ReconciliationIntervalHours { get; set; } = 6;

    /// <summary>Delay after host start before the first reconciliation run (does not block Kestrel).</summary>
    public int ReconciliationStartupDelaySeconds { get; set; } = 30;

    /// <summary>PostgreSQL read batch size per entity type during reconciliation.</summary>
    public int ReconciliationBatchSize { get; set; } = 200;

    /// <summary>Maximum wall-clock time for one reconciliation run before cancellation.</summary>
    public int ReconciliationRunTimeoutMinutes { get; set; } = 45;

    /// <summary>How often <see cref="SearchOutboxProcessorHostedService"/> polls pending outbox rows.</summary>
    public int OutboxPollIntervalSeconds { get; set; } = 5;

    /// <summary>Log a warning when pending outbox depth exceeds this threshold (§6.5 observability).</summary>
    public int OutboxWarningPendingCount { get; set; } = 1000;

    /// <summary>
    /// True when operators explicitly enabled search and supplied a usable worker URL. Used by DI to decide whether to open a <see cref="Grpc.Net.Client.GrpcChannel"/>.
    /// </summary>
    public bool IsWorkerAddressValid =>
        !string.IsNullOrWhiteSpace(WorkerGrpcUrl) &&
        Uri.TryCreate(WorkerGrpcUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Whether the gRPC client should be constructed at startup.</summary>
    public bool IsEnabled => Enabled && IsWorkerAddressValid;
}
