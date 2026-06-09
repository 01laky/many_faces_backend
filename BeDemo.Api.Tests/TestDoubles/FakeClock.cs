using BeDemo.Api.Services;

namespace BeDemo.Api.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="IClock"/> for time-dependent tests (token TTLs, retention cutoffs, lockout windows).
/// Backend-refactor v1 Phase 0 (§10.1/§5.3): replaces real <c>Task.Delay</c>/<c>DateTime.UtcNow</c> coupling so
/// time-sensitive assertions are instant and stable.
/// </summary>
public sealed class FakeClock : IClock
{
	public FakeClock(DateTime utcNow) => UtcNow = utcNow;

	public DateTime UtcNow { get; set; }

	/// <summary>Move the clock forward (or back, with a negative span).</summary>
	public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
