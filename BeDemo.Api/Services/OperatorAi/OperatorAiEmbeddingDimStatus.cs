namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Singleton snapshot of the startup embedding-dimension probe (<see cref="OperatorAiEmbeddingDimStartupAssertion"/>),
/// so the admin AI overview can show whether the worker's embedding model matches <c>AiService:EmbeddingDim</c>
/// (and thus the pinned ES <c>dense_vector</c> mapping). A drift means RAG indexing is rejected by the search
/// worker, so this is a health signal worth surfacing. Thread-safe via a volatile swap of an immutable record.
/// </summary>
public sealed class OperatorAiEmbeddingDimStatus
{
	/// <summary><see cref="Ok"/> is null until the probe has run (worker still loading / AI disabled / unreachable).</summary>
	public sealed record Snapshot(bool? Ok, int? Actual, DateTime? CheckedAtUtc);

	private volatile Snapshot _current = new(Ok: null, Actual: null, CheckedAtUtc: null);

	public Snapshot Current => _current;

	/// <summary>Record a completed probe: <paramref name="ok"/> = (actual == configured EmbeddingDim).</summary>
	public void Record(bool ok, int actual) => _current = new Snapshot(ok, actual, DateTime.UtcNow);
}
