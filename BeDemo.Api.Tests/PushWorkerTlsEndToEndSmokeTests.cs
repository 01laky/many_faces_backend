using BeDemo.Api.Services;
using FluentAssertions;
using Grpc.Net.Client;

namespace BeDemo.Api.Tests;

/// <summary>
/// Optional Docker-backed TLS + mTLS smoke for the push-worker: run only when <c>PUSH_TLS_SMOKE=1</c>
/// (see <c>many_faces_push/scripts/smoke-grpc-tls.sh</c>).
/// </summary>
public sealed class PushWorkerTlsEndToEndSmokeTests
{
    [Fact]
    public async Task Grpc_health_check_succeeds_when_push_tls_smoke_env_set()
    {
        if (Environment.GetEnvironmentVariable("PUSH_TLS_SMOKE") != "1")
        {
            return;
        }

        var port = Environment.GetEnvironmentVariable("PUSH_TLS_SMOKE_GRPC_PORT") ?? "59215";
        var ca = Environment.GetEnvironmentVariable("PUSH_TLS_SMOKE_CA");
        var clientCert = Environment.GetEnvironmentVariable("PUSH_TLS_SMOKE_CLIENT_CERT");
        var clientKey = Environment.GetEnvironmentVariable("PUSH_TLS_SMOKE_CLIENT_KEY");

        ca.Should().NotBeNullOrWhiteSpace("PUSH_TLS_SMOKE_CA");
        clientCert.Should().NotBeNullOrWhiteSpace("PUSH_TLS_SMOKE_CLIENT_CERT");
        clientKey.Should().NotBeNullOrWhiteSpace("PUSH_TLS_SMOKE_CLIENT_KEY");
        File.Exists(ca!).Should().BeTrue();
        File.Exists(clientCert!).Should().BeTrue();
        File.Exists(clientKey!).Should().BeTrue();

        var o = new PushOptions
        {
            Enabled = true,
            WorkerGrpcUrl = $"https://127.0.0.1:{port}",
            WorkerTlsServerCaPath = ca,
            WorkerTlsClientCertPath = clientCert,
            WorkerTlsClientKeyPath = clientKey,
            WorkerGrpcTlsServerName = "localhost",
            GrpcDeadlineSeconds = 45,
        };

        var dispose = new List<System.Security.Cryptography.X509Certificates.X509Certificate2>();
        using var ch = GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(o), dispose);
        var health = new global::Grpc.Health.V1.Health.HealthClient(ch);
        var resp = await health.CheckAsync(new global::Grpc.Health.V1.HealthCheckRequest());
        resp.Status.Should().Be(global::Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Serving);
    }
}
