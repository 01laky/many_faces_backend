using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Services.OperatorMail;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="MailerService.MailerServiceClient"/> (many_faces_mailer). Returns null from send/test when
/// operator mail settings disallow sends.
/// </summary>
public sealed class MailerWorkerGrpcClient : IMailerWorkerClient, IDisposable
{
    private readonly IOperatorMailSettingsProvider _settings;
    private readonly IOptions<MailOptions> _envOptions;
    private readonly ILogger<MailerWorkerGrpcClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly object _channelLock = new();
    private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];
    private ActiveChannel? _active;

    public MailerWorkerGrpcClient(
        IOperatorMailSettingsProvider settings,
        IOptions<MailOptions> envOptions,
        ILogger<MailerWorkerGrpcClient> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _settings = settings;
        _envOptions = envOptions;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!runtime.IsSendAllowed)
            return null;

        request = OperatorMailProtoMapper.EnrichRequest(request, runtime);
        var client = await GetClientAsync(runtime, cancellationToken).ConfigureAwait(false);
        if (client is null)
            return null;

        var callOptions = BuildCallOptions(runtime, cancellationToken);
        try
        {
            return await client.SendTemplatedEmailAsync(request, callOptions).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Mailer worker SendTemplatedEmail failed: {Code} {Detail}", ex.StatusCode, ex.Status.Detail);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TestSmtpConnectionResponse?> TestSmtpConnectionAsync(
        OperatorMailSettingsValues settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsWorkerAddressValid)
            return new TestSmtpConnectionResponse { Reachable = false, Detail = "Worker gRPC URL invalid." };

        var client = await GetClientAsync(settings, cancellationToken).ConfigureAwait(false);
        if (client is null)
            return new TestSmtpConnectionResponse { Reachable = false, Detail = "Mail worker client unavailable." };

        var req = new TestSmtpConnectionRequest { Smtp = OperatorMailProtoMapper.ToProto(settings) };
        var callOptions = BuildCallOptions(settings, cancellationToken);
        return await client.TestSmtpConnectionAsync(req, callOptions).ConfigureAwait(false);
    }

    private async Task<MailerService.MailerServiceClient?> GetClientAsync(
        OperatorMailSettingsValues runtime,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!runtime.IsWorkerAddressValid)
            return null;

        var cacheKey = runtime.ChannelCacheKey;
        lock (_channelLock)
        {
            if (_active?.CacheKey == cacheKey)
                return _active.Client;

            _active?.Dispose();
            var merged = MergeTlsMailOptions(runtime);
            var channel = GrpcWorkerChannelFactory.CreateChannel(
                GrpcWorkerChannelFactory.FromMail(merged),
                _tlsCertificatesToDispose);
            _active = new ActiveChannel(cacheKey, channel, new MailerService.MailerServiceClient(channel));
            return _active.Client;
        }
    }

    private MailOptions MergeTlsMailOptions(OperatorMailSettingsValues runtime)
    {
        var env = _envOptions.Value;
        return new MailOptions
        {
            Enabled = runtime.Enabled,
            WorkerGrpcUrl = runtime.WorkerGrpcUrl,
            WorkerAuthToken = runtime.WorkerAuthTokenPlaintext,
            WorkerTlsServerCaPath = env.WorkerTlsServerCaPath,
            WorkerTlsClientCertPath = env.WorkerTlsClientCertPath,
            WorkerTlsClientKeyPath = env.WorkerTlsClientKeyPath,
            WorkerGrpcTlsServerName = env.WorkerGrpcTlsServerName,
            GrpcDeadlineSeconds = env.GrpcDeadlineSeconds,
        };
    }

    private CallOptions BuildCallOptions(OperatorMailSettingsValues runtime, CancellationToken cancellationToken)
    {
        var headers = new Metadata();
        MailerWorkerCorrelationMetadata.AppendFromHttpHeaders(_httpContextAccessor.HttpContext?.Request.Headers, headers);
        if (!string.IsNullOrWhiteSpace(runtime.WorkerAuthTokenPlaintext))
            headers.Add("x-mailer-worker-token", runtime.WorkerAuthTokenPlaintext.Trim());

        var deadlineSeconds = Math.Clamp(_envOptions.Value.GrpcDeadlineSeconds, 1, 120);
        return new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_channelLock)
        {
            _active?.Dispose();
            _active = null;
        }

        foreach (var c in _tlsCertificatesToDispose)
            c.Dispose();

        _tlsCertificatesToDispose.Clear();
    }

    private sealed class ActiveChannel(string cacheKey, GrpcChannel channel, MailerService.MailerServiceClient client) : IDisposable
    {
        public string CacheKey { get; } = cacheKey;
        public MailerService.MailerServiceClient Client { get; } = client;
        private readonly GrpcChannel _channel = channel;

        public void Dispose() => _channel.Dispose();
    }
}
