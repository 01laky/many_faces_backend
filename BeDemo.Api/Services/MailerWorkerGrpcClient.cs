using BeDemo.Api.Services.OperatorMail;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="MailerService.MailerServiceClient"/> (many_faces_mailer). Returns null from send/test when
/// operator mail settings disallow sends. Channel cache and disposal are handled by <see cref="WorkerGrpcClientBase{TClient}"/>.
/// </summary>
public sealed class MailerWorkerGrpcClient : WorkerGrpcClientBase<MailerService.MailerServiceClient>, IMailerWorkerClient
{
	private readonly IOperatorMailSettingsProvider _settings;
	private readonly IOptions<MailOptions> _envOptions;
	private readonly ILogger<MailerWorkerGrpcClient> _logger;
	private readonly IHttpContextAccessor _httpContextAccessor;

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
		var client = GetClientOrNull(runtime);
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

		var client = GetClientOrNull(settings);
		if (client is null)
			return new TestSmtpConnectionResponse { Reachable = false, Detail = "Mail worker client unavailable." };

		var req = new TestSmtpConnectionRequest { Smtp = OperatorMailProtoMapper.ToProto(settings) };
		var callOptions = BuildCallOptions(settings, cancellationToken);
		return await client.TestSmtpConnectionAsync(req, callOptions).ConfigureAwait(false);
	}

	private MailerService.MailerServiceClient? GetClientOrNull(OperatorMailSettingsValues runtime)
	{
		if (!runtime.IsWorkerAddressValid)
			return null;

		var merged = MergeTlsMailOptions(runtime);
		return GetOrReplaceClient(
			runtime.ChannelCacheKey,
			() => GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromMail(merged), CertificatesToDispose),
			ch => new MailerService.MailerServiceClient(ch));
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
}
