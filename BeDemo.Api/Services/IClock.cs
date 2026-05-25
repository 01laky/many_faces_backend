namespace BeDemo.Api.Services;

/// <summary>
/// Abstraction over wall-clock time so OAuth token body signature canonical strings can be tested deterministically.
/// </summary>
public interface IClock
{
	/// <summary>Current UTC instant used when building the canonical signature payload.</summary>
	DateTime UtcNow { get; }
}

/// <summary>Production implementation: delegates to <see cref="DateTime.UtcNow"/>.</summary>
public sealed class SystemUtcClock : IClock
{
	/// <inheritdoc />
	public DateTime UtcNow => DateTime.UtcNow;
}
