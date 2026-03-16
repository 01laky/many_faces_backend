/*
 * AiGrpcService.cs - gRPC client for Python AI service (ai_demo)
 *
 * Calls the Generate RPC on the AI service. Address is read from configuration
 * (AiService:GrpcAddress or AI_SERVICE_GRPC_ADDRESS), e.g. http://ai-demo-dev:50051.
 * Recreates the channel on connection failures (e.g. after AI container restart).
 */

using Grpc.Core;
using Grpc.Net.Client;
using Health;

namespace BeDemo.Api.Services;

/// <summary>
/// Singleton gRPC client that calls the Python AI service Generate RPC.
/// Recreates the channel when connection fails (e.g. AI container restarted).
/// </summary>
public class AiGrpcService : IAiGrpcService, IDisposable
{
    private readonly string _grpcAddress;
    private readonly ILogger<AiGrpcService> _logger;
    private readonly object _channelLock = new();
    private GrpcChannel? _channel;
    private readonly TimeSpan _deadline = TimeSpan.FromSeconds(300);

    public AiGrpcService(IConfiguration configuration, ILogger<AiGrpcService> logger)
    {
        _grpcAddress = configuration["AiService:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
            ?? "http://ai-demo-dev:50051";
        _logger = logger;
    }

    private GrpcChannel GetOrCreateChannel()
    {
        lock (_channelLock)
        {
            if (_channel != null)
                return _channel;
            _logger.LogInformation("Creating gRPC channel to AI service at {Address}", _grpcAddress);
            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            };
            _channel = GrpcChannel.ForAddress(_grpcAddress, new GrpcChannelOptions { HttpHandler = handler });
            return _channel;
        }
    }

    private void InvalidateChannel()
    {
        lock (_channelLock)
        {
            if (_channel != null)
            {
                try { _channel.Dispose(); } catch { /* ignore */ }
                _channel = null;
                _logger.LogInformation("gRPC channel invalidated, will recreate on next request");
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(string prompt, int maxNewTokens = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        var request = new GenerateRequest
        {
            Prompt = prompt,
            MaxNewTokens = maxNewTokens <= 0 ? 50 : maxNewTokens
        };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_deadline);
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(_deadline), cancellationToken: cts.Token);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                _logger.LogInformation("Sending Generate request (attempt {Attempt}), prompt length={Length}", attempt, prompt.Length);
                var response = await client.GenerateAsync(request, callOptions);
                _logger.LogInformation("Received Generate response, text length={Length}", response.Text?.Length ?? 0);

                if (!string.IsNullOrEmpty(response.Error))
                {
                    _logger.LogWarning("AI Generate returned error: {Error}", response.Error);
                    return response.Error;
                }
                return response.Text ?? string.Empty;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "gRPC Unavailable (attempt {Attempt}), invalidating channel", attempt);
                InvalidateChannel();
                if (attempt == 2)
                {
                    _logger.LogError("gRPC call failed after retry: {Detail}", ex.Status.Detail);
                    return $"Error: AI service unavailable ({ex.StatusCode})";
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC call failed: Status={Status}", ex.StatusCode);
                return $"Error: AI service unavailable ({ex.StatusCode})";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error (attempt {Attempt}), invalidating channel", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return "Error: AI service unavailable (connection failed)";
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI Generate timed out after {Seconds}s", _deadline.TotalSeconds);
                return "Error: AI service timed out";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Generate failed");
                return $"Error: {ex.Message}";
            }
        }
        return "Error: AI service unavailable";
    }

    public void Dispose()
    {
        lock (_channelLock)
        {
            _channel?.Dispose();
            _channel = null;
        }
    }
}
