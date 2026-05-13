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
    /// Plaintext HTTP/2 (h2c) is acceptable only on trusted dev Docker networks; production should use TLS and stronger auth (see many_faces_elastic README).
    /// </summary>
    public string? WorkerGrpcUrl { get; set; }

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
