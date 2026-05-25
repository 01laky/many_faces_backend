using BeDemo.Api.Services;
using FluentAssertions;
using Grpc.Net.Client;

namespace BeDemo.Api.Tests;

/// <summary>
/// Optional Docker-backed TLS + mTLS smoke for the mailer-worker: run only when <c>MAILER_TLS_SMOKE=1</c>
/// (see <c>many_faces_mailer/scripts/smoke-grpc-tls.sh</c>).
/// </summary>
public sealed class MailerWorkerTlsEndToEndSmokeTests
{
	[Fact]
	public async Task Grpc_health_check_succeeds_when_mailer_tls_smoke_env_set()
	{
		if (Environment.GetEnvironmentVariable("MAILER_TLS_SMOKE") != "1")
		{
			return;
		}

		var port = Environment.GetEnvironmentVariable("MAILER_TLS_SMOKE_GRPC_PORT") ?? "59216";
		var ca = Environment.GetEnvironmentVariable("MAILER_TLS_SMOKE_CA");
		var clientCert = Environment.GetEnvironmentVariable("MAILER_TLS_SMOKE_CLIENT_CERT");
		var clientKey = Environment.GetEnvironmentVariable("MAILER_TLS_SMOKE_CLIENT_KEY");

		ca.Should().NotBeNullOrWhiteSpace("MAILER_TLS_SMOKE_CA");
		clientCert.Should().NotBeNullOrWhiteSpace("MAILER_TLS_SMOKE_CLIENT_CERT");
		clientKey.Should().NotBeNullOrWhiteSpace("MAILER_TLS_SMOKE_CLIENT_KEY");
		File.Exists(ca!).Should().BeTrue();
		File.Exists(clientCert!).Should().BeTrue();
		File.Exists(clientKey!).Should().BeTrue();

		var o = new MailOptions
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
		using var ch = GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromMail(o), dispose);
		var health = new global::Grpc.Health.V1.Health.HealthClient(ch);
		var resp = await health.CheckAsync(new global::Grpc.Health.V1.HealthCheckRequest());
		resp.Status.Should().Be(global::Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Serving);
	}
}
