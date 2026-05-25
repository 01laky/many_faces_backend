namespace BeDemo.Api.Configuration;

using Microsoft.Extensions.Configuration;

/// <summary>Python AI worker gRPC client settings (BSH3-G1 — TLS + service token parity with Search/Push/Mail).</summary>
public sealed class AiServiceOptions
{
	public const string SectionName = "AiService";

	/// <summary>gRPC server address, e.g. <c>http://ai-demo-dev:50051</c> (dev h2c) or <c>https://...</c> (Hardened).</summary>
	public string GrpcAddress { get; set; } = "http://ai-demo-dev:50051";

	/// <summary>Optional PEM for private CA when <see cref="GrpcAddress"/> uses <c>https://</c>.</summary>
	public string? WorkerTlsServerCaPath { get; set; }

	/// <summary>Optional mTLS client cert PEM.</summary>
	public string? WorkerTlsClientCertPath { get; set; }

	/// <summary>Optional mTLS client key PEM.</summary>
	public string? WorkerTlsClientKeyPath { get; set; }

	/// <summary>Optional TLS hostname override for certificate validation.</summary>
	public string? WorkerGrpcTlsServerName { get; set; }

	/// <summary>Shared secret sent as gRPC metadata (header name <see cref="WorkerAuthMetadataKey"/>).</summary>
	public string? WorkerAuthToken { get; set; }

	/// <summary>Metadata key for AI worker bearer token (align with many_faces_ai when server auth is enabled).</summary>
	public const string WorkerAuthMetadataKey = "x-ai-worker-token";

	/// <summary>When true, refresh host profile from the worker on backend startup.</summary>
	public bool HostProfileRefreshOnStartup { get; set; } = true;

	/// <summary>Max seconds to retry GetHostProfile during startup before giving up.</summary>
	public int HostProfileStartupTimeoutSeconds { get; set; } = 30;

	/// <summary>Resolved address: config, then <c>AI_SERVICE_GRPC_ADDRESS</c> env var.</summary>
	public string ResolveGrpcAddress(IConfiguration configuration) =>
		configuration["AiService:GrpcAddress"]
		?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
		?? GrpcAddress;
}
