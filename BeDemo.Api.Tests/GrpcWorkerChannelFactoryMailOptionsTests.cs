using BeDemo.Api.Services;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// TLS / h2c validation parity for <see cref="MailOptions"/> through <see cref="GrpcWorkerChannelFactory.FromMail"/>.
/// </summary>
public sealed class GrpcWorkerChannelFactoryMailOptionsTests
{
    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_allows_plain_http_without_tls_keys()
    {
        var o = new MailOptions { WorkerGrpcUrl = "http://mailer-worker-dev:50054" };
        var act = () => GrpcWorkerChannelFactory.ValidateHttpUrlHasNoTlsOptions(GrpcWorkerChannelFactory.FromMail(o));
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHttpUrlHasNoTlsOptions_throws_when_tls_server_ca_set()
    {
        var o = new MailOptions
        {
            WorkerGrpcUrl = "http://mailer-worker:50054",
            WorkerTlsServerCaPath = "/tmp/ca.pem",
        };
        var act = () => GrpcWorkerChannelFactory.ValidateHttpUrlHasNoTlsOptions(GrpcWorkerChannelFactory.FromMail(o));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mail*https://*");
    }
}
