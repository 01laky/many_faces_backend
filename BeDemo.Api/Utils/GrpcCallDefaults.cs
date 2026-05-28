using BeDemo.Api.Configuration;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Utils;

/// <summary>BE-RP32 — shared gRPC call deadlines.</summary>
public static class GrpcCallDefaults
{
	public static CallOptions SearchCallOptions(
		IOptions<PerformanceOptions> options,
		CancellationToken cancellationToken = default)
	{
		var seconds = Math.Max(1, options.Value.SearchGrpcDeadlineSeconds);
		return new CallOptions(deadline: DateTime.UtcNow.AddSeconds(seconds), cancellationToken: cancellationToken);
	}
}
