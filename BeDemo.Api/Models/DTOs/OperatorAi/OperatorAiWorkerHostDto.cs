using System.Text.Json;

namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiWorkerHostDto
{
	public bool Reachable { get; set; }

	public DateTime? LastRefreshAttemptUtc { get; set; }

	public string? LastRefreshError { get; set; }

	public string? GrpcAddressConfigured { get; set; }

	/// <summary>Parsed host profile JSON or null when never collected.</summary>
	public JsonElement? Profile { get; set; }

	/// <summary>
	/// Backend-side AI config (not in the worker snapshot) so the admin overview is complete even when the
	/// worker is unreachable: the decision-helper / embedding model names + the embedding-dim ↔ ES health.
	/// </summary>
	public OperatorAiConfigDto? Config { get; set; }
}

/// <summary>Backend-composed AI configuration for the admin overview (from AiServiceOptions + the startup dim probe).</summary>
public sealed class OperatorAiConfigDto
{
	/// <summary>CPU-resident decision-helper model (AiService:HelperModel); null when the helper is disabled.</summary>
	public string? HelperModel { get; set; }

	/// <summary>Whether the 3B decision helper is active (HelperForDecisions &amp;&amp; a HelperModel is configured).</summary>
	public bool HelperEnabled { get; set; }

	/// <summary>Embedding model the backend pins for RAG (AiService:EmbeddingModel).</summary>
	public string? EmbeddingModel { get; set; }

	/// <summary>Embedding vector dimension the backend + ES dense_vector mapping are pinned to.</summary>
	public int EmbeddingDim { get; set; }

	/// <summary>Startup-probe result: true/false once the probe ran, null while pending (worker loading / AI off).</summary>
	public bool? EmbeddingDimOk { get; set; }

	/// <summary>Actual vector length the worker's embed model returned at the last probe (null until probed).</summary>
	public int? EmbeddingDimActual { get; set; }
}
