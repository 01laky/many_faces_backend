using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Push.V1;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="PushService.PushServiceClient"/> (many_faces_push). Returns null from send/test when
/// operator push settings disallow sends.
/// </summary>
public sealed class PushWorkerGrpcClient : IPushWorkerClient, IDisposable
{
	private readonly IOperatorPushSettingsProvider _settings;
	private readonly IOptions<PushOptions> _envOptions;
	private readonly ILogger<PushWorkerGrpcClient> _logger;
	private readonly object _channelLock = new();
	private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];
	private ActiveChannel? _active;

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
		var client = await GetClientAsync(runtime, cancellationToken).ConfigureAwait(false);
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

		var client = await GetClientAsync(settings, cancellationToken).ConfigureAwait(false);
		if (client is null)
			return new TestFcmCredentialsResponse { Valid = false, Detail = "Push worker client unavailable." };

		var req = new TestFcmCredentialsRequest
		{
			Fcm = OperatorPushProtoMapper.ToProto(settings),
		};
		var callOptions = BuildCallOptions(settings, cancellationToken);
		return await client.TestFcmCredentialsAsync(req, callOptions).ConfigureAwait(false);
	}

	private async Task<PushService.PushServiceClient?> GetClientAsync(
		OperatorPushSettingsValues runtime,
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
			var merged = MergeTlsPushOptions(runtime);
			var channel = GrpcWorkerChannelFactory.CreateChannel(
				GrpcWorkerChannelFactory.FromPush(merged),
				_tlsCertificatesToDispose);
			_active = new ActiveChannel(cacheKey, channel, new PushService.PushServiceClient(channel));
			return _active.Client;
		}
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

	private sealed class ActiveChannel(string cacheKey, GrpcChannel channel, PushService.PushServiceClient client) : IDisposable
	{
		public string CacheKey { get; } = cacheKey;
		public PushService.PushServiceClient Client { get; } = client;
		private readonly GrpcChannel _channel = channel;

		public void Dispose() => _channel.Dispose();
	}
}
