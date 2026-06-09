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

	// ── Embeddings (RAG retrieval v1, §5.5) ───────────────────────────────────
	// Single source of truth for the embedding model + dimension. The backend
	// embeds descriptors (index plane) and operator questions (query plane) via
	// the AI worker EmbedText RPC; the Go search-worker creates the ES dense_vector
	// mapping with EmbeddingDim and rejects any KnowledgeDocument whose vector_dim
	// differs — so backend, worker and ES mapping can never silently drift.

	/// <summary>Ollama embedding model name (must match <c>OLLAMA_MODEL_EMBED</c> on the worker), e.g. <c>nomic-embed-text</c>.</summary>
	public string EmbeddingModel { get; set; } = "nomic-embed-text";

	/// <summary>Embedding vector dimension the worker/ES mapping is pinned to (e.g. 768 for nomic-embed-text). Startup probe asserts this.</summary>
	public int EmbeddingDim { get; set; } = 768;

	/// <summary>When true (default), run the startup probe that embeds a fixed string and asserts the returned length == <see cref="EmbeddingDim"/> (fail loud on drift).</summary>
	public bool AssertEmbeddingDimOnStartup { get; set; } = true;

	// ── 7B performance: optional CPU-resident helper model (7B-perf O19) ───────
	// A small model (e.g. qwen2.5:3b) the worker loads CPU-only (num_gpu=0) alongside
	// the GPU-resident 7B, used only for small routing/gating decisions (advisory; it
	// never changes numbers). Empty ⇒ helper disabled, everything falls back to the
	// deterministic heuristics. Must match OLLAMA_MODEL_HELPER on the worker.

	/// <summary>O19 — optional helper model name (e.g. <c>qwen2.5:3b-instruct-q4_K_M</c>). Empty disables the helper.</summary>
	public string? HelperModel { get; set; }

	/// <summary>O19 — generation budget (ms) for a helper decision call (kept short — single-label classification on CPU).</summary>
	public int HelperTimeoutMs { get; set; } = 6_000;

	/// <summary>When true, refresh host profile from the worker on backend startup.</summary>
	public bool HostProfileRefreshOnStartup { get; set; } = true;

	/// <summary>O10 — issue a tiny throwaway Generate at startup (num_predict=1) to force the model fully loaded + GPU layers allocated, so the first operator turn isn't cold. Best-effort, time-boxed.</summary>
	public bool WarmUpGenerationOnStartup { get; set; } = true;

	/// <summary>O10 — max seconds to spend on the startup warm-up Generate before giving up (never blocks boot).</summary>
	public int WarmUpStartupTimeoutSeconds { get; set; } = 60;

	/// <summary>Max seconds to retry GetHostProfile during startup before giving up.</summary>
	public int HostProfileStartupTimeoutSeconds { get; set; } = 30;

	/// <summary>Resolved address: config, then <c>AI_SERVICE_GRPC_ADDRESS</c> env var.</summary>
	public string ResolveGrpcAddress(IConfiguration configuration) =>
		configuration["AiService:GrpcAddress"]
		?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
		?? GrpcAddress;
}
