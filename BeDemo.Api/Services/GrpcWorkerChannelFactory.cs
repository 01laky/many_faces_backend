using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Configuration;
using Grpc.Net.Client;

namespace BeDemo.Api.Services;

/// <summary>
/// Shared <see cref="GrpcChannel"/> construction for internal workers (search, push, mailer) so TLS and h2c rules stay consistent.
/// </summary>
internal static class GrpcWorkerChannelFactory
{
    internal static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>TLS-related fields copied from <see cref="SearchOptions"/> / <see cref="PushOptions"/> at call time.</summary>
    internal readonly record struct GrpcWorkerTlsSettings(
        string WorkerGrpcUrl,
        string? WorkerTlsServerCaPath,
        string? WorkerTlsClientCertPath,
        string? WorkerTlsClientKeyPath,
        string? WorkerGrpcTlsServerName,
        string OptionsSectionPrefixForErrors);

    internal static GrpcWorkerTlsSettings FromSearch(SearchOptions o) => new(
        o.WorkerGrpcUrl!.Trim(),
        o.WorkerTlsServerCaPath,
        o.WorkerTlsClientCertPath,
        o.WorkerTlsClientKeyPath,
        o.WorkerGrpcTlsServerName,
        "Search");

    internal static GrpcWorkerTlsSettings FromPush(PushOptions o) => new(
        o.WorkerGrpcUrl!.Trim(),
        o.WorkerTlsServerCaPath,
        o.WorkerTlsClientCertPath,
        o.WorkerTlsClientKeyPath,
        o.WorkerGrpcTlsServerName,
        "Push");

    internal static GrpcWorkerTlsSettings FromMail(MailOptions o) => new(
        o.WorkerGrpcUrl!.Trim(),
        o.WorkerTlsServerCaPath,
        o.WorkerTlsClientCertPath,
        o.WorkerTlsClientKeyPath,
        o.WorkerGrpcTlsServerName,
        "Mail");

    internal static GrpcWorkerTlsSettings FromAi(AiServiceOptions o, string resolvedGrpcAddress) => new(
        resolvedGrpcAddress.Trim(),
        o.WorkerTlsServerCaPath,
        o.WorkerTlsClientCertPath,
        o.WorkerTlsClientKeyPath,
        o.WorkerGrpcTlsServerName,
        "AiService");

    internal static GrpcChannel CreateChannel(
        GrpcWorkerTlsSettings s,
        List<X509Certificate2> disposeList,
        X509RevocationMode revocationMode = X509RevocationMode.NoCheck)
    {
        var uri = new Uri(s.WorkerGrpcUrl, UriKind.Absolute);

        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 4 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024,
            DisposeHttpClient = true,
        };

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            ValidateHttpUrlHasNoTlsOptions(s);
            return GrpcChannel.ForAddress(uri, channelOptions);
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{s.OptionsSectionPrefixForErrors}:WorkerGrpcUrl must use http:// or https://.");
        }

        var caPath = TrimOrNull(s.WorkerTlsServerCaPath);
        var clientCertPath = TrimOrNull(s.WorkerTlsClientCertPath);
        var clientKeyPath = TrimOrNull(s.WorkerTlsClientKeyPath);
        var tlsServerName = TrimOrNull(s.WorkerGrpcTlsServerName);

        if (clientCertPath is null ^ clientKeyPath is null)
        {
            throw new InvalidOperationException(
                $"{s.OptionsSectionPrefixForErrors}:WorkerTlsClientCertPath and {s.OptionsSectionPrefixForErrors}:WorkerTlsClientKeyPath must both be set for mTLS, or both left empty.");
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
                throw new InvalidOperationException(
                    $"{s.OptionsSectionPrefixForErrors}:WorkerTlsServerCaPath did not contain any PEM certificates: {caPath}");
            }

            foreach (X509Certificate2 c in customRoots)
            {
                disposeList.Add(c);
            }

            var roots = customRoots;
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, sslPolicyErrors) =>
                ValidateServerCertWithCustomRoots(certificate, sslPolicyErrors, roots, revocationMode);
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

    internal static void ValidateHttpUrlHasNoTlsOptions(GrpcWorkerTlsSettings s)
    {
        if (TrimOrNull(s.WorkerTlsServerCaPath) is not null
            || TrimOrNull(s.WorkerTlsClientCertPath) is not null
            || TrimOrNull(s.WorkerTlsClientKeyPath) is not null
            || TrimOrNull(s.WorkerGrpcTlsServerName) is not null)
        {
            throw new InvalidOperationException(
                $"{s.OptionsSectionPrefixForErrors} TLS options ({s.OptionsSectionPrefixForErrors}:WorkerTls*, {s.OptionsSectionPrefixForErrors}:WorkerGrpcTlsServerName) apply only when {s.OptionsSectionPrefixForErrors}:WorkerGrpcUrl uses https://.");
        }
    }

    internal static bool ValidateServerCertWithCustomRoots(
        X509Certificate? certificate,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2Collection customRoots,
        X509RevocationMode revocationMode = X509RevocationMode.NoCheck)
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
        chain.ChainPolicy.RevocationMode = revocationMode;
        return chain.Build(server);
    }
}
