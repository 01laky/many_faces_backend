using System.Diagnostics;
using BeDemo.Api.Models.DTOs;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Production implementation of <see cref="ISearchWorkerProbe"/> using <see cref="GrpcChannel"/> and the generated
/// <see cref="SearchService.SearchServiceClient"/>. The channel is created once per process when search is enabled so connection
/// pooling and HTTP/2 negotiation behave as recommended by Microsoft gRPC client guidance.
/// </summary>
public sealed class SearchWorkerGrpcProbe : ISearchWorkerProbe, IDisposable
{
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchWorkerGrpcProbe> _logger;
    private readonly GrpcChannel? _channel;
    private readonly global::ManyFaces.Search.V1.SearchService.SearchServiceClient? _client;

    /// <summary>
    /// Captures options and eagerly builds a gRPC channel when <see cref="SearchOptions.IsEnabled"/> is true.
    /// </summary>
    public SearchWorkerGrpcProbe(IOptions<SearchOptions> options, ILogger<SearchWorkerGrpcProbe> logger)
    {
        _options = options;
        _logger = logger;
        var o = options.Value;
        if (!o.IsEnabled)
        {
            return;
        }

        // ForAddress validates the URI scheme early; invalid URLs should have made IsEnabled false.
        _channel = GrpcChannel.ForAddress(o.WorkerGrpcUrl!, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 4 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024,
        });
        _client = new global::ManyFaces.Search.V1.SearchService.SearchServiceClient(_channel);
    }

    /// <inheritdoc />
    public async Task<SearchHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var o = _options.Value;

        if (!o.Enabled)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search is disabled (Search:Enabled is false).",
            };
        }

        if (string.IsNullOrWhiteSpace(o.WorkerGrpcUrl))
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search is not configured (set Search:WorkerGrpcUrl to the Go worker, e.g. http://search-worker-dev:50052).",
            };
        }

        if (!o.IsWorkerAddressValid)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search:WorkerGrpcUrl must be an absolute http or https URL.",
            };
        }

        if (_client is null)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search gRPC client is not initialized.",
            };
        }

        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(o.WorkerAuthToken))
        {
            // Header name matches many_faces_elastic internal/server/auth_interceptor.go (case-insensitive on the wire).
            headers.Add("x-search-worker-token", o.WorkerAuthToken.Trim());
        }

        var deadlineSeconds = Math.Clamp(o.GrpcDeadlineSeconds, 1, 120);
        var callOptions = new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);

        try
        {
            var correlation = Activity.Current?.Id ?? string.Empty;
            var response = await _client.PingAsync(new PingRequest { CorrelationId = correlation }, callOptions);

            if (!response.ElasticsearchReachable)
            {
                return new SearchHealthDto
                {
                    Configured = true,
                    Reachable = false,
                    ClusterName = string.IsNullOrWhiteSpace(response.ClusterName) ? null : response.ClusterName,
                    Message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                        ? "Search worker reports Elasticsearch unreachable."
                        : response.ErrorMessage,
                };
            }

            return new SearchHealthDto
            {
                Configured = true,
                Reachable = true,
                ClusterName = string.IsNullOrWhiteSpace(response.ClusterName) ? null : response.ClusterName,
                Message = null,
            };
        }
        catch (RpcException ex)
        {
            _logger.LogDebug(ex, "Search worker gRPC Ping failed");
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = false,
                ClusterName = null,
                Message = ex.StatusCode + ": " + (string.IsNullOrWhiteSpace(ex.Status.Detail) ? ex.Message : ex.Status.Detail),
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Search worker gRPC Ping failed");
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = false,
                ClusterName = null,
                Message = ex.Message,
            };
        }
    }

    /// <inheritdoc />
    public void Dispose() => _channel?.Dispose();
}
