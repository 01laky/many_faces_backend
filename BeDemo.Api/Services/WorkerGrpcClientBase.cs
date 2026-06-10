using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;

namespace BeDemo.Api.Services;

/// <summary>
/// Shared channel-cache and disposal skeleton for gRPC worker clients (push, mailer).
/// Concrete clients own the settings-merge and call-options logic; this base handles the lock + cached-channel +
/// dispose pattern that is structurally identical across all operator worker gRPC clients.
///
/// Threading: a single lock serialises channel builds and cache reads. The cached client is reused for the
/// lifetime of a given settings fingerprint (<paramref name="cacheKey"/> in <see cref="GetOrReplaceClient"/>).
/// </summary>
public abstract class WorkerGrpcClientBase<TClient> : IDisposable
	where TClient : class
{
	private readonly object _lock = new();

	/// <summary>
	/// TLS leaf/client certificates loaded during channel builds by the concrete subclass lambda; disposed together
	/// on <see cref="Dispose"/>. Pass to <c>GrpcWorkerChannelFactory.CreateChannel</c> in the buildChannel lambda.
	/// </summary>
	protected readonly List<X509Certificate2> CertificatesToDispose = [];

	private CachedChannel? _cached;

	/// <summary>
	/// Returns the cached gRPC client when <paramref name="cacheKey"/> matches the active channel, or disposes the
	/// stale channel and creates a new one via <paramref name="buildChannel"/> + <paramref name="buildClient"/>.
	/// Thread-safe via internal lock.
	/// </summary>
	protected TClient GetOrReplaceClient(
		string cacheKey,
		Func<GrpcChannel> buildChannel,
		Func<GrpcChannel, TClient> buildClient)
	{
		lock (_lock)
		{
			if (_cached?.CacheKey == cacheKey)
				return _cached.Client;

			_cached?.Dispose();
			var channel = buildChannel();
			_cached = new CachedChannel(cacheKey, channel, buildClient(channel));
			return _cached.Client;
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_cached?.Dispose();
			_cached = null;
		}

		foreach (var c in CertificatesToDispose)
			c.Dispose();

		CertificatesToDispose.Clear();
	}

	private sealed class CachedChannel(string cacheKey, GrpcChannel channel, TClient client) : IDisposable
	{
		public string CacheKey { get; } = cacheKey;
		public TClient Client { get; } = client;
		private readonly GrpcChannel _channel = channel;

		public void Dispose() => _channel.Dispose();
	}
}
