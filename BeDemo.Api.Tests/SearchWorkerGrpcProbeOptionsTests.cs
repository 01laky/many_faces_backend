using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Fast unit tests for <see cref="SearchWorkerGrpcProbe"/> TLS option validation (no Docker / no live worker).
/// </summary>
public sealed class SearchWorkerGrpcProbeOptionsTests
{
    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_allows_plain_http_without_tls_keys()
    {
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "http://search-worker-dev:50052",
        };

        var act = () => SearchWorkerGrpcProbe.ValidateHttpUrlHasNoTlsOptions(o);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_throws_when_tls_server_ca_set()
    {
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "http://search-worker:50052",
            WorkerTlsServerCaPath = "/tmp/ca.pem",
        };

        var act = () => SearchWorkerGrpcProbe.ValidateHttpUrlHasNoTlsOptions(o);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*https://*");
    }

    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_throws_when_client_cert_path_set()
    {
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "http://search-worker:50052",
            WorkerTlsClientCertPath = "/tmp/client.crt",
        };

        var act = () => SearchWorkerGrpcProbe.ValidateHttpUrlHasNoTlsOptions(o);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_throws_when_tls_server_name_set()
    {
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "http://127.0.0.1:50052",
            WorkerGrpcTlsServerName = "search.internal",
        };

        var act = () => SearchWorkerGrpcProbe.ValidateHttpUrlHasNoTlsOptions(o);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateGrpcChannel_throws_when_https_and_only_client_cert_without_key()
    {
        var dispose = new List<X509Certificate2>();
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "https://search-worker:50052",
            WorkerTlsClientCertPath = "/tmp/a.crt",
            WorkerTlsClientKeyPath = "",
        };

        var act = () => SearchWorkerGrpcProbe.CreateGrpcChannel(o, dispose);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*WorkerTlsClientCertPath*WorkerTlsClientKeyPath*");
    }

    [Fact]
    public void CreateGrpcChannel_throws_when_https_and_only_client_key_without_cert()
    {
        var dispose = new List<X509Certificate2>();
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "https://search-worker:50052",
            WorkerTlsClientCertPath = "",
            WorkerTlsClientKeyPath = "/tmp/a.key",
        };

        var act = () => SearchWorkerGrpcProbe.CreateGrpcChannel(o, dispose);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateGrpcChannel_throws_when_custom_ca_file_missing()
    {
        var dispose = new List<X509Certificate2>();
        var o = new SearchOptions
        {
            WorkerGrpcUrl = "https://127.0.0.1:50052",
            WorkerTlsServerCaPath = Path.Combine(Path.GetTempPath(), $"missing-ca-{Guid.NewGuid():N}.pem"),
        };

        var act = () => SearchWorkerGrpcProbe.CreateGrpcChannel(o, dispose);
        act.Should().Throw<Exception>(); // FileNotFoundException or IOException depending on OS
    }

    [Fact]
    public void CreateGrpcChannel_throws_when_custom_ca_pem_has_no_certificates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"empty-ca-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, "# empty\nnot a pem block\n");
        try
        {
            var dispose = new List<X509Certificate2>();
            var o = new SearchOptions
            {
                WorkerGrpcUrl = "https://127.0.0.1:50052",
                WorkerTlsServerCaPath = path,
            };

            var act = () => SearchWorkerGrpcProbe.CreateGrpcChannel(o, dispose);
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
        var o = new SearchOptions { WorkerGrpcUrl = "http://127.0.0.1:9" };

        using var ch = SearchWorkerGrpcProbe.CreateGrpcChannel(o, dispose);
        ch.Should().NotBeNull();
        dispose.Should().BeEmpty();
    }

    [Fact]
    public void ValidateServerCertWithCustomRoots_returns_false_for_null_certificate()
    {
        var roots = new X509Certificate2Collection();
        SearchWorkerGrpcProbe.ValidateServerCertWithCustomRoots(null, SslPolicyErrors.RemoteCertificateChainErrors, roots)
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateServerCertWithCustomRoots_returns_true_when_no_ssl_errors()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=noop", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var self = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        var roots = new X509Certificate2Collection();

        SearchWorkerGrpcProbe.ValidateServerCertWithCustomRoots(self, SslPolicyErrors.None, roots)
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateServerCertWithCustomRoots_returns_false_on_name_mismatch_even_with_ca_in_store()
    {
        using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var caReq = new CertificateRequest("CN=test-ca", caKey, HashAlgorithmName.SHA256);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var ca = caReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));

        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafReq = new CertificateRequest("CN=wrong-host", leafKey, HashAlgorithmName.SHA256);
        using var leaf = leafReq.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(8));

        var roots = new X509Certificate2Collection { ca };
        var err = SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;

        SearchWorkerGrpcProbe.ValidateServerCertWithCustomRoots(leaf, err, roots)
            .Should().BeFalse("name mismatch is not masked by custom root trust");
    }

    [Fact]
    public void SearchWorkerGrpcProbe_ctor_throws_on_invalid_https_tls_combo()
    {
        var o = Options.Create(new SearchOptions
        {
            Enabled = true,
            WorkerGrpcUrl = "https://search-worker:50052",
            WorkerTlsClientCertPath = "/no/such/client.crt",
            WorkerTlsClientKeyPath = "/no/such/client.key",
        });

        var act = () => _ = new SearchWorkerGrpcProbe(o, NullLogger<SearchWorkerGrpcProbe>.Instance);
        act.Should().Throw<IOException>();
    }
}
