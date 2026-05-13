using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Optional Docker-backed TLS + mTLS smoke: run only when <c>SEARCH_TLS_SMOKE=1</c> (see <c>many_faces_elastic/scripts/smoke-grpc-tls.sh</c>).
/// Default <c>dotnet test</c> without that environment variable exits this test immediately so local runs stay fast.
/// </summary>
public sealed class SearchWorkerTlsEndToEndSmokeTests
{
    [Fact]
    public async Task Grpc_ping_reaches_elasticsearch_when_search_tls_smoke_env_set()
    {
        if (Environment.GetEnvironmentVariable("SEARCH_TLS_SMOKE") != "1")
        {
            return;
        }

        var port = Environment.GetEnvironmentVariable("SEARCH_TLS_SMOKE_GRPC_PORT") ?? "59211";
        var ca = Environment.GetEnvironmentVariable("SEARCH_TLS_SMOKE_CA");
        var clientCert = Environment.GetEnvironmentVariable("SEARCH_TLS_SMOKE_CLIENT_CERT");
        var clientKey = Environment.GetEnvironmentVariable("SEARCH_TLS_SMOKE_CLIENT_KEY");

        ca.Should().NotBeNullOrWhiteSpace("SEARCH_TLS_SMOKE_CA");
        clientCert.Should().NotBeNullOrWhiteSpace("SEARCH_TLS_SMOKE_CLIENT_CERT");
        clientKey.Should().NotBeNullOrWhiteSpace("SEARCH_TLS_SMOKE_CLIENT_KEY");
        File.Exists(ca!).Should().BeTrue();
        File.Exists(clientCert!).Should().BeTrue();
        File.Exists(clientKey!).Should().BeTrue();

        var options = Options.Create(new SearchOptions
        {
            Enabled = true,
            WorkerGrpcUrl = $"https://127.0.0.1:{port}",
            WorkerTlsServerCaPath = ca,
            WorkerTlsClientCertPath = clientCert,
            WorkerTlsClientKeyPath = clientKey,
            WorkerGrpcTlsServerName = "localhost",
            GrpcDeadlineSeconds = 45,
        });

        using var probe = new SearchWorkerGrpcProbe(options, NullLogger<SearchWorkerGrpcProbe>.Instance);
        var health = await probe.GetHealthAsync();
        health.Configured.Should().BeTrue();
        health.Reachable.Should().BeTrue("worker Ping should see Elasticsearch in the smoke compose stack");
        health.ClusterName.Should().NotBeNullOrWhiteSpace();
    }
}
