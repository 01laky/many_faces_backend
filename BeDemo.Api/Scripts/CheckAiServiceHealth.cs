using Grpc.Net.Client;
using Serilog;

namespace BeDemo.Api.Scripts;

/**
 * CheckAiServiceHealth - Checks AI service health via gRPC
 * 
 * This class provides functionality to check if the AI Demo gRPC service is running
 * and ready to accept requests. It connects to the gRPC server and calls the HealthCheck
 * RPC method to verify service availability.
 * 
 * Note: This is a simplified implementation that checks connectivity.
 * For a full implementation, generate C# gRPC client code from health.proto
 * and use the HealthService.HealthCheck RPC method.
 */
public static class CheckAiServiceHealth
{
    /**
     * Checks the health of the AI Demo gRPC service
     * 
     * Attempts to connect to the gRPC server at the specified address.
     * Since we don't have the generated gRPC client code yet, this performs
     * a basic connectivity check. In production, this should call the actual
     * HealthCheck RPC method.
     * 
     * @param grpcAddress - The gRPC server address (e.g., "http://ai-demo-dev:50051")
     * @param timeoutSeconds - Timeout in seconds for the health check (default: 10)
     * @return True if health check succeeds, false otherwise
     */
    public static async Task<bool> CheckHealthAsync(string grpcAddress, int timeoutSeconds = 10)
    {
        try
        {
            Log.Information("Checking AI service health at {GrpcAddress}", grpcAddress);
            
            // Create gRPC channel with timeout
            // Note: For development, we use insecure channel (no TLS)
            // In production, use proper TLS credentials
            using var channel = GrpcChannel.ForAddress(grpcAddress, new GrpcChannelOptions
            {
                // Use insecure channel for development (use TLS in production)
                HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                }
            });
            
            // For now, we perform a basic connectivity check
            // TODO: Generate C# gRPC client code from health.proto using:
            //   protoc --csharp_out=. --grpc_out=. --plugin=protoc-gen-grpc=grpc_csharp_plugin health.proto
            // Then use: var client = new HealthService.HealthServiceClient(channel);
            // var response = await client.HealthCheckAsync(new HealthCheckRequest());
            // return response.Status == "success";
            
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            
            // Basic connectivity check - try to connect to the gRPC server
            // gRPC uses HTTP/2, so we can verify the channel is ready
            await channel.ConnectAsync(cancellationTokenSource.Token);
            
            Log.Information("AI service health check passed at {GrpcAddress}", grpcAddress);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AI service health check failed at {GrpcAddress}. Service may not be ready yet.", grpcAddress);
            return false;
        }
    }
}
