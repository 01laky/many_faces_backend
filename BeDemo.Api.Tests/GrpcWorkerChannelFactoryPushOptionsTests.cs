using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Fast unit tests for <see cref="GrpcWorkerChannelFactory"/> with <see cref="PushOptions"/> (TLS parity with search-worker tests).
/// </summary>
public sealed class GrpcWorkerChannelFactoryPushOptionsTests
{
	[Fact]
	public void ValidateHttpUrlHasNoTlsOptions_allows_plain_http_without_tls_keys()
	{
		var o = new PushOptions { WorkerGrpcUrl = "http://push-worker-dev:50053" };
		var act = () => GrpcWorkerChannelFactory.ValidateHttpUrlHasNoTlsOptions(GrpcWorkerChannelFactory.FromPush(o));
		act.Should().NotThrow();
	}

	[Fact]
	public void ValidateHttpUrlHasNoTlsOptions_throws_when_tls_server_ca_set()
	{
		var o = new PushOptions
		{
			WorkerGrpcUrl = "http://push-worker:50053",
			WorkerTlsServerCaPath = "/tmp/ca.pem",
		};
		var act = () => GrpcWorkerChannelFactory.ValidateHttpUrlHasNoTlsOptions(GrpcWorkerChannelFactory.FromPush(o));
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*Push*https://*");
	}

	[Fact]
	public void CreateGrpcChannel_throws_when_https_and_only_client_cert_without_key()
	{
		var dispose = new List<X509Certificate2>();
		var o = new PushOptions
		{
			WorkerGrpcUrl = "https://push-worker:50053",
			WorkerTlsClientCertPath = "/tmp/a.crt",
			WorkerTlsClientKeyPath = "",
		};
		var act = () => GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(o), dispose);
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*Push*WorkerTlsClientCertPath*WorkerTlsClientKeyPath*");
	}

	[Fact]
	public void CreateGrpcChannel_throws_when_custom_ca_pem_has_no_certificates()
	{
		var path = Path.Combine(Path.GetTempPath(), $"empty-ca-push-{Guid.NewGuid():N}.pem");
		File.WriteAllText(path, "# empty\nnot a pem block\n");
		try
		{
			var dispose = new List<X509Certificate2>();
			var o = new PushOptions
			{
				WorkerGrpcUrl = "https://127.0.0.1:50053",
				WorkerTlsServerCaPath = path,
			};
			var act = () => GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(o), dispose);
			act.Should().Throw<InvalidOperationException>()
				.WithMessage("*did not contain any PEM certificates*");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void CreateGrpcChannel_plaintext_when_http_url()
	{
		var dispose = new List<X509Certificate2>();
		var o = new PushOptions { WorkerGrpcUrl = "http://127.0.0.1:9" };
		using var ch = GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(o), dispose);
		ch.Should().NotBeNull();
		dispose.Should().BeEmpty();
	}

	[Fact]
	public void ValidateServerCertWithCustomRoots_push_prefix_uses_shared_factory()
	{
		var roots = new X509Certificate2Collection();
		GrpcWorkerChannelFactory.ValidateServerCertWithCustomRoots(null, SslPolicyErrors.RemoteCertificateChainErrors, roots)
			.Should().BeFalse();
	}

	[Fact]
	public async Task PushWorkerGrpcClient_send_throws_on_invalid_https_tls_combo()
	{
		var values = new OperatorPushSettingsValues(
			true,
			"https://push-worker:50053",
			null,
			"demo-project",
			IntegrationTestPush.TestFirebaseServiceAccountJson,
			"push_test_title",
			"push_test_body",
			null,
			15,
			DateTime.UtcNow,
			null);
		var provider = new StaticPushSettingsProvider(values);
		var o = Options.Create(new PushOptions
		{
			WorkerTlsClientCertPath = "/no/such/client.crt",
			WorkerTlsClientKeyPath = "/no/such/client.key",
		});
		using var sut = new PushWorkerGrpcClient(provider, o, NullLogger<PushWorkerGrpcClient>.Instance);
		var act = async () =>
		{
			var req = new ManyFaces.Push.V1.SendPushRequest { TitleLocKey = "k", BodyLocKey = "b" };
			req.RegistrationTokens.Add("tok");
			await sut.SendPushAsync(req);
		};
		await act.Should().ThrowAsync<IOException>();
	}
}
