using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Models.DTOs;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Production implementation of <see cref="ISearchWorkerProbe"/> using <see cref="GrpcChannel"/> and the generated
/// <see cref="SearchService.SearchServiceClient"/>. The channel is created once per process when search is enabled so connection
/// pooling and HTTP/2 negotiation behave as recommended by Microsoft gRPC client guidance.
/// </summary>
public sealed class SearchWorkerGrpcProbe : ISearchWorkerProbe, IDisposable
{
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchWorkerGrpcProbe> _logger;
    private readonly GrpcChannel? _channel;
    private readonly global::ManyFaces.Search.V1.SearchService.SearchServiceClient? _client;
    private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];

    /// <summary>
    /// Captures options and eagerly builds a gRPC channel when <see cref="SearchOptions.IsEnabled"/> is true.
    /// </summary>
    public SearchWorkerGrpcProbe(IOptions<SearchOptions> options, ILogger<SearchWorkerGrpcProbe> logger)
    {
        _options = options;
        _logger = logger;
        var o = options.Value;
        if (!o.IsEnabled)
        {
            return;
        }

        _channel = CreateGrpcChannel(o, _tlsCertificatesToDispose);
        _client = new global::ManyFaces.Search.V1.SearchService.SearchServiceClient(_channel);
    }

    internal static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Builds a <see cref="GrpcChannel"/> for the configured worker URL. Exposed for unit tests (see <c>BeDemo.Api.Tests</c>).
    /// </summary>
    internal static GrpcChannel CreateGrpcChannel(SearchOptions o, List<X509Certificate2> disposeList)
    {
        var uri = new Uri(o.WorkerGrpcUrl!.Trim(), UriKind.Absolute);

        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 4 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024,
            DisposeHttpClient = true,
        };

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            ValidateHttpUrlHasNoTlsOptions(o);
            return GrpcChannel.ForAddress(uri, channelOptions);
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Search:WorkerGrpcUrl must use http:// or https://.");
        }

        var caPath = TrimOrNull(o.WorkerTlsServerCaPath);
        var clientCertPath = TrimOrNull(o.WorkerTlsClientCertPath);
        var clientKeyPath = TrimOrNull(o.WorkerTlsClientKeyPath);
        var tlsServerName = TrimOrNull(o.WorkerGrpcTlsServerName);

        if (clientCertPath is null ^ clientKeyPath is null)
        {
            throw new InvalidOperationException(
                "Search:WorkerTlsClientCertPath and Search:WorkerTlsClientKeyPath must both be set for mTLS, or both left empty.");
        }

        var needsCustomHandler = caPath is not null || clientCertPath is not null || tlsServerName is not null;
        if (!needsCustomHandler)
        {
            return GrpcChannel.ForAddress(uri, channelOptions);
        }

        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
            SslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = tlsServerName ?? uri.Host,
            },
        };

        if (caPath is not null)
        {
            var pem = File.ReadAllText(caPath);
            var customRoots = new X509Certificate2Collection();
            customRoots.ImportFromPem(pem);
            if (customRoots.Count == 0)
            {
                throw new InvalidOperationException($"Search:WorkerTlsServerCaPath did not contain any PEM certificates: {caPath}");
            }

            foreach (X509Certificate2 c in customRoots)
            {
                disposeList.Add(c);
            }

            var roots = customRoots;
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, sslPolicyErrors) =>
                ValidateServerCertWithCustomRoots(certificate, sslPolicyErrors, roots);
        }

        if (clientCertPath is not null && clientKeyPath is not null)
        {
            var clientCert = X509Certificate2.CreateFromPemFile(clientCertPath, clientKeyPath);
            disposeList.Add(clientCert);
            handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
            handler.SslOptions.ClientCertificates.Add(clientCert);
        }

        channelOptions.HttpHandler = handler;
        return GrpcChannel.ForAddress(uri, channelOptions);
    }

    internal static void ValidateHttpUrlHasNoTlsOptions(SearchOptions o)
    {
        if (TrimOrNull(o.WorkerTlsServerCaPath) is not null
            || TrimOrNull(o.WorkerTlsClientCertPath) is not null
            || TrimOrNull(o.WorkerTlsClientKeyPath) is not null
            || TrimOrNull(o.WorkerGrpcTlsServerName) is not null)
        {
            throw new InvalidOperationException(
                "Search TLS options (Search:WorkerTls*, Search:WorkerGrpcTlsServerName) apply only when Search:WorkerGrpcUrl uses https://.");
        }
    }

    internal static bool ValidateServerCertWithCustomRoots(
        X509Certificate? certificate,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2Collection customRoots)
    {
        if (certificate is not X509Certificate2 server)
        {
            return false;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(customRoots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(server);
    }

    /// <inheritdoc />
    public async Task<SearchHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var o = _options.Value;

        if (!o.Enabled)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search is disabled (Search:Enabled is false).",
            };
        }

        if (string.IsNullOrWhiteSpace(o.WorkerGrpcUrl))
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search is not configured (set Search:WorkerGrpcUrl to the Go worker, e.g. http://search-worker-dev:50052).",
            };
        }

        if (!o.IsWorkerAddressValid)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search:WorkerGrpcUrl must be an absolute http or https URL.",
            };
        }

        if (_client is null)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search gRPC client is not initialized.",
            };
        }

        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(o.WorkerAuthToken))
        {
            // Header name matches many_faces_elastic internal/server/auth_interceptor.go (case-insensitive on the wire).
            headers.Add("x-search-worker-token", o.WorkerAuthToken.Trim());
        }

        var deadlineSeconds = Math.Clamp(o.GrpcDeadlineSeconds, 1, 120);
        var callOptions = new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);

        try
        {
            var correlation = Activity.Current?.Id ?? string.Empty;
            var response = await _client.PingAsync(new PingRequest { CorrelationId = correlation }, callOptions);

            if (!response.ElasticsearchReachable)
            {
                return new SearchHealthDto
                {
                    Configured = true,
                    Reachable = false,
                    ClusterName = string.IsNullOrWhiteSpace(response.ClusterName) ? null : response.ClusterName,
                    Message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                        ? "Search worker reports Elasticsearch unreachable."
                        : response.ErrorMessage,
                };
            }

            return new SearchHealthDto
            {
                Configured = true,
                Reachable = true,
                ClusterName = string.IsNullOrWhiteSpace(response.ClusterName) ? null : response.ClusterName,
                Message = null,
            };
        }
        catch (RpcException ex)
        {
            _logger.LogDebug(ex, "Search worker gRPC Ping failed");
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = false,
                ClusterName = null,
                Message = ex.StatusCode + ": " + (string.IsNullOrWhiteSpace(ex.Status.Detail) ? ex.Message : ex.Status.Detail),
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Search worker gRPC Ping failed");
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = false,
                ClusterName = null,
                Message = ex.Message,
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _channel?.Dispose();
        foreach (var c in _tlsCertificatesToDispose)
        {
            c.Dispose();
        }

        _tlsCertificatesToDispose.Clear();
    }
}
