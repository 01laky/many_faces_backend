/*
 * AiGrpcService.cs - gRPC client for Python AI service (ai_demo)
 *
 * Calls the Generate RPC on the AI service. Address is read from configuration
 * (AiService:GrpcAddress or AI_SERVICE_GRPC_ADDRESS), e.g. http://ai-demo-dev:50051.
 */

using Grpc.Net.Client;
using Health;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client that calls the Python AI service Generate RPC.
/// </summary>
public class AiGrpcService : IAiGrpcService
{
    private readonly string _grpcAddress;
    private readonly ILogger<AiGrpcService> _logger;
    private GrpcChannel? _channel;
    private HealthService.HealthServiceClient? _client;

    public AiGrpcService(IConfiguration configuration, ILogger<AiGrpcService> logger)
    {
        _grpcAddress = configuration["AiService:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
            ?? "http://ai-demo-dev:50051";
        _logger = logger;
    }

    /// <summary>
    /// Lazy-init gRPC channel and client (created on first call).
    /// </summary>
    private HealthService.HealthServiceClient GetClient()
    {
        if (_client != null)
            return _client;

        _logger.LogInformation("Creating gRPC channel to AI service at {Address}", _grpcAddress);
        _channel = GrpcChannel.ForAddress(_grpcAddress, new GrpcChannelOptions { });
        _client = new HealthService.HealthServiceClient(_channel);
        return _client;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(string prompt, int maxNewTokens = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        try
        {
            var client = GetClient();
            var request = new GenerateRequest
            {
                Prompt = prompt,
                MaxNewTokens = maxNewTokens <= 0 ? 50 : maxNewTokens
            };
            var response = await client.GenerateAsync(request, cancellationToken: cancellationToken);

            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogWarning("AI Generate returned error: {Error}", response.Error);
                return response.Error;
            }

            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Generate failed for prompt length {Length}", prompt?.Length ?? 0);
            return $"Error: {ex.Message}";
        }
    }
}
