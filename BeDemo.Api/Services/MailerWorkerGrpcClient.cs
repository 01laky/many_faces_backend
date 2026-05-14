using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="MailerService.MailerServiceClient"/> (many_faces_mailer). Returns null from <see cref="SendTemplatedEmailAsync"/>
/// when <see cref="MailOptions.IsEnabled"/> is false so callers skip work without exceptions — same ergonomics as <see cref="PushWorkerGrpcClient"/>.
/// </summary>
public sealed class MailerWorkerGrpcClient : IMailerWorkerClient, IDisposable
{
    private readonly IOptions<MailOptions> _options;
    private readonly ILogger<MailerWorkerGrpcClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GrpcChannel? _channel;
    private readonly MailerService.MailerServiceClient? _client;
    private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];

    public MailerWorkerGrpcClient(
        IOptions<MailOptions> options,
        ILogger<MailerWorkerGrpcClient> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _options = options;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        var o = options.Value;
        if (!o.IsEnabled)
        {
            return;
        }

        _channel = GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromMail(o), _tlsCertificatesToDispose);
        _client = new MailerService.MailerServiceClient(_channel);
    }

    /// <inheritdoc />
    public async Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        if (!o.IsEnabled || _client is null)
        {
            return null;
        }

        var headers = new Metadata();
        MailerWorkerCorrelationMetadata.AppendFromHttpHeaders(_httpContextAccessor.HttpContext?.Request.Headers, headers);
        if (!string.IsNullOrWhiteSpace(o.WorkerAuthToken))
        {
            // Header name matches many_faces_mailer MailerAuthInterceptor (parity with x-push-worker-token).
            headers.Add("x-mailer-worker-token", o.WorkerAuthToken.Trim());
        }

        var deadlineSeconds = Math.Clamp(o.GrpcDeadlineSeconds, 1, 120);
        var callOptions = new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);

        try
        {
            return await _client.SendTemplatedEmailAsync(request, callOptions).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Mailer worker SendTemplatedEmail failed: {Code} {Detail}", ex.StatusCode, ex.Status.Detail);
            throw;
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
