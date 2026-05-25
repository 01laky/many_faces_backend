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
			// Note: For development, we use HTTP (insecure) channel (no TLS)
			// In production, use HTTPS with proper TLS credentials
			// Grpc.Net.Client automatically uses insecure channel for HTTP addresses
			using var channel = GrpcChannel.ForAddress(grpcAddress, new GrpcChannelOptions
			{
				// For HTTP endpoints, Grpc.Net.Client automatically uses insecure channel
				// No need to explicitly set credentials for HTTP
			});

			var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

			// Basic connectivity check - try to connect to the gRPC server
			// gRPC uses HTTP/2, so we can verify the channel is ready
			// This checks if the server is listening on the specified port
			// ConnectAsync() will throw if the server is not reachable
			await channel.ConnectAsync(cancellationTokenSource.Token);

			// Note: For a full implementation, we would generate C# gRPC client code from health.proto:
			//   1. Install protoc compiler and grpc_csharp_plugin
			//   2. Run: protoc --csharp_out=. --grpc_out=. --plugin=protoc-gen-grpc=grpc_csharp_plugin proto/health.proto
			//   3. Then use: var client = new HealthService.HealthServiceClient(channel);
			//   4. var response = await client.HealthCheckAsync(new HealthCheckRequest(), cancellationToken: cancellationTokenSource.Token);
			//   5. return response.Status == "success";
			// For now, we just verify connectivity

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
