namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Singleton cache of the (unit-normalized) skill descriptor vectors used for routing (§4). It persists the warmed
/// vectors across requests so the scoped <see cref="IOperatorAiSkillRouter"/> does not re-embed the 4 descriptors
/// on every turn. Re-warms when the embed model changes. Thread-safe (single-flight warm).
/// </summary>
public interface IOperatorAiSkillVectorCache
{
	/// <summary>
	/// Return the cached skill-id → vector map, embedding the descriptors once on first use (or after a model
	/// change). Returns null when embedding is unavailable (the router then falls back to general-assistant).
	/// </summary>
	Task<IReadOnlyDictionary<string, float[]>?> GetOrWarmAsync(
		IReadOnlyList<(string Id, string Text)> descriptors,
		string model,
		Func<string, CancellationToken, Task<float[]?>> embed,
		CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class OperatorAiSkillVectorCache : IOperatorAiSkillVectorCache
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private Dictionary<string, float[]>? _vectors;
	private string? _model;

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, float[]>?> GetOrWarmAsync(
		IReadOnlyList<(string Id, string Text)> descriptors,
		string model,
		Func<string, CancellationToken, Task<float[]?>> embed,
		CancellationToken cancellationToken)
	{
		if (_vectors is { Count: > 0 } && string.Equals(_model, model, StringComparison.Ordinal))
			return _vectors;

		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (_vectors is { Count: > 0 } && string.Equals(_model, model, StringComparison.Ordinal))
				return _vectors;

			var built = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
			foreach (var (id, text) in descriptors)
			{
				var vec = await embed(text, cancellationToken);
				if (vec is not null)
					built[id] = vec;
			}

			if (built.Count == 0)
				return null; // embedding unavailable — caller falls back to general-assistant

			_vectors = built;
			_model = model;
			return _vectors;
		}
		finally
		{
			_gate.Release();
		}
	}
}
