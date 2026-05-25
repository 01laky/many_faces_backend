using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>
/// Production <see cref="ISearchQueryGateway"/> using <see cref="GrpcWorkerChannelFactory"/> (mirrors <see cref="SearchWorkerGrpcProbe"/>).
/// </summary>
public sealed class SearchWorkerGrpcGateway : ISearchQueryGateway, IDisposable
{
	private readonly IOptions<SearchOptions> _options;
	private readonly ILogger<SearchWorkerGrpcGateway> _logger;
	private readonly GrpcChannel? _channel;
	private readonly SearchService.SearchServiceClient? _client;
	private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];

	public SearchWorkerGrpcGateway(IOptions<SearchOptions> options, ILogger<SearchWorkerGrpcGateway> logger)
	{
		_options = options;
		_logger = logger;
		var o = options.Value;
		if (!o.IsEnabled)
			return;

		_channel = SearchWorkerGrpcProbe.CreateGrpcChannel(o, _tlsCertificatesToDispose);
		_client = new SearchService.SearchServiceClient(_channel);
	}

	/// <inheritdoc />
	public bool IsAvailable => _client is not null && _options.Value.IsEnabled;

	/// <inheritdoc />
	public Task<AutocompleteResponse?> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken = default) =>
		InvokeUnaryAsync(
			callOptions => _client!.AutocompleteAsync(request, callOptions),
			cancellationToken);

	/// <inheritdoc />
	public Task<IndexDocumentResponse?> IndexDocumentAsync(IndexDocumentRequest request, CancellationToken cancellationToken = default) =>
		InvokeUnaryAsync(
			callOptions => _client!.IndexDocumentAsync(request, callOptions),
			cancellationToken);

	/// <inheritdoc />
	public Task<DeleteDocumentResponse?> DeleteDocumentAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default) =>
		InvokeUnaryAsync(
			callOptions => _client!.DeleteDocumentAsync(request, callOptions),
			cancellationToken);

	/// <inheritdoc />
	public Task<BulkIndexDocumentsResponse?> BulkIndexDocumentsAsync(BulkIndexDocumentsRequest request, CancellationToken cancellationToken = default) =>
		InvokeUnaryAsync(
			callOptions => _client!.BulkIndexDocumentsAsync(request, callOptions),
			cancellationToken);

	/// <inheritdoc />
	public Task<ListDocumentIdsResponse?> ListDocumentIdsAsync(ListDocumentIdsRequest request, CancellationToken cancellationToken = default) =>
		InvokeUnaryAsync(
			callOptions => _client!.ListDocumentIdsAsync(request, callOptions),
			cancellationToken);

	private async Task<TResponse?> InvokeUnaryAsync<TResponse>(
		Func<CallOptions, AsyncUnaryCall<TResponse>> callFactory,
		CancellationToken cancellationToken)
		where TResponse : class
	{
		if (_client is null)
			return null;

		try
		{
			return await callFactory(BuildCallOptions(cancellationToken));
		}
		catch (RpcException ex)
		{
			_logger.LogDebug(ex, "Search worker gRPC call failed: {Code}", ex.StatusCode);
			throw;
		}
	}

	private CallOptions BuildCallOptions(CancellationToken cancellationToken)
	{
		var o = _options.Value;
		var headers = new Metadata();
		if (!string.IsNullOrWhiteSpace(o.WorkerAuthToken))
			headers.Add("x-search-worker-token", o.WorkerAuthToken.Trim());

		var deadlineSeconds = Math.Clamp(o.GrpcDeadlineSeconds, 1, 120);
		return new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);
	}

	internal static string NewCorrelationId() =>
		Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

	/// <inheritdoc />
	public void Dispose()
	{
		_channel?.Dispose();
		foreach (var c in _tlsCertificatesToDispose)
			c.Dispose();
		_tlsCertificatesToDispose.Clear();
	}
}
