using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Production <see cref="ISearchWorkerKnowledgeClient"/> using <see cref="SearchWorkerGrpcProbe.CreateGrpcChannel"/>
/// (same channel/TLS/auth as <c>SearchWorkerGrpcGateway</c> and the autocomplete path, §8).
///
/// <para>Why a dedicated client (not an extension of the query gateway):</para>
/// The operator-AI knowledge RPCs are an internal RAG surface (D7); keeping them on a separate client keeps the
/// user-facing search gateway focused and lets DI register/swap retrieval independently. The channel is created
/// once per process when search is enabled (connection pooling + HTTP/2 negotiation as recommended).
///
/// <para>Inputs/outputs:</para>
/// Each method sets a correlation id when missing, forwards the <c>x-search-worker-token</c> header, and returns
/// <c>null</c> on disabled/unreachable (callers degrade to the planner). RpcExceptions propagate so the retriever
/// can log + fall back; the indexer treats them as a failed rebuild.
/// </summary>
public sealed class SearchWorkerKnowledgeClient : ISearchWorkerKnowledgeClient, IDisposable
{
	private readonly IOptions<SearchOptions> _options;
	private readonly ILogger<SearchWorkerKnowledgeClient> _logger;
	private readonly GrpcChannel? _channel;
	private readonly SearchService.SearchServiceClient? _client;
	private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];

	public SearchWorkerKnowledgeClient(IOptions<SearchOptions> options, ILogger<SearchWorkerKnowledgeClient> logger)
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
	public Task<IndexKnowledgeResponse?> IndexKnowledgeAsync(IndexKnowledgeRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(request.CorrelationId))
			request.CorrelationId = NewCorrelationId();
		return InvokeAsync(callOptions => _client!.IndexKnowledgeAsync(request, callOptions), cancellationToken);
	}

	/// <inheritdoc />
	public Task<DeleteKnowledgeResponse?> DeleteKnowledgeAsync(DeleteKnowledgeRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(request.CorrelationId))
			request.CorrelationId = NewCorrelationId();
		return InvokeAsync(callOptions => _client!.DeleteKnowledgeAsync(request, callOptions), cancellationToken);
	}

	/// <inheritdoc />
	public Task<SemanticSearchResponse?> SemanticSearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(request.CorrelationId))
			request.CorrelationId = NewCorrelationId();
		return InvokeAsync(callOptions => _client!.SemanticSearchAsync(request, callOptions), cancellationToken);
	}

	/// <inheritdoc />
	public Task<KnowledgeIndexStatusResponse?> KnowledgeIndexStatusAsync(KnowledgeIndexStatusRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(request.CorrelationId))
			request.CorrelationId = NewCorrelationId();
		return InvokeAsync(callOptions => _client!.KnowledgeIndexStatusAsync(request, callOptions), cancellationToken);
	}

	private async Task<TResponse?> InvokeAsync<TResponse>(
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
			_logger.LogDebug(ex, "Knowledge gRPC call failed: {Code}", ex.StatusCode);
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
