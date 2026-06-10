using BeDemo.Api.Services.OperatorPush;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Push.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="PushService.PushServiceClient"/> (many_faces_push). Returns null from send/test when
/// operator push settings disallow sends. Channel cache and disposal are handled by <see cref="WorkerGrpcClientBase{TClient}"/>.
/// </summary>
public sealed class PushWorkerGrpcClient : WorkerGrpcClientBase<PushService.PushServiceClient>, IPushWorkerClient
{
	private readonly IOperatorPushSettingsProvider _settings;
	private readonly IOptions<PushOptions> _envOptions;
	private readonly ILogger<PushWorkerGrpcClient> _logger;

	public PushWorkerGrpcClient(
		IOperatorPushSettingsProvider settings,
		IOptions<PushOptions> envOptions,
		ILogger<PushWorkerGrpcClient> logger)
	{
		_settings = settings;
		_envOptions = envOptions;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<SendPushResponse?> SendPushAsync(SendPushRequest request, CancellationToken cancellationToken = default)
	{
		var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!runtime.IsSendAllowed)
			return null;

		request = OperatorPushProtoMapper.EnrichRequest(request, runtime);
		var client = GetClientOrNull(runtime);
		if (client is null)
			return null;

		var callOptions = BuildCallOptions(runtime, cancellationToken);
		try
		{
			return await client.SendPushAsync(request, callOptions).ConfigureAwait(false);
		}
		catch (RpcException ex)
		{
			_logger.LogWarning(ex, "Push worker SendPush failed: {Code} {Detail}", ex.StatusCode, ex.Status.Detail);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<TestFcmCredentialsResponse?> TestFcmCredentialsAsync(
		OperatorPushSettingsValues settings,
		CancellationToken cancellationToken = default)
	{
		if (!settings.IsWorkerAddressValid)
			return new TestFcmCredentialsResponse { Valid = false, Detail = "Worker gRPC URL invalid." };

		if (!settings.HasFirebaseCredentials)
			return new TestFcmCredentialsResponse { Valid = false, Detail = "Firebase credentials missing." };

		var client = GetClientOrNull(settings);
		if (client is null)
			return new TestFcmCredentialsResponse { Valid = false, Detail = "Push worker client unavailable." };

		var req = new TestFcmCredentialsRequest
		{
			Fcm = OperatorPushProtoMapper.ToProto(settings),
		};
		var callOptions = BuildCallOptions(settings, cancellationToken);
		return await client.TestFcmCredentialsAsync(req, callOptions).ConfigureAwait(false);
	}

	private PushService.PushServiceClient? GetClientOrNull(OperatorPushSettingsValues runtime)
	{
		if (!runtime.IsWorkerAddressValid)
			return null;

		var merged = MergeTlsPushOptions(runtime);
		return GetOrReplaceClient(
			runtime.ChannelCacheKey,
			() => GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(merged), CertificatesToDispose),
			ch => new PushService.PushServiceClient(ch));
	}

	private PushOptions MergeTlsPushOptions(OperatorPushSettingsValues runtime)
	{
		var env = _envOptions.Value;
		return new PushOptions
		{
			Enabled = runtime.Enabled,
			WorkerGrpcUrl = runtime.WorkerGrpcUrl,
			WorkerAuthToken = runtime.WorkerAuthTokenPlaintext,
			WorkerTlsServerCaPath = env.WorkerTlsServerCaPath,
			WorkerTlsClientCertPath = env.WorkerTlsClientCertPath,
			WorkerTlsClientKeyPath = env.WorkerTlsClientKeyPath,
			WorkerGrpcTlsServerName = env.WorkerGrpcTlsServerName,
			GrpcDeadlineSeconds = runtime.GrpcDeadlineSeconds,
		};
	}

	private CallOptions BuildCallOptions(OperatorPushSettingsValues runtime, CancellationToken cancellationToken)
	{
		var headers = new Metadata();
		if (!string.IsNullOrWhiteSpace(runtime.WorkerAuthTokenPlaintext))
			headers.Add("x-push-worker-token", runtime.WorkerAuthTokenPlaintext.Trim());

		var deadlineSeconds = Math.Clamp(runtime.GrpcDeadlineSeconds, 1, 120);
		return new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);
	}
}
