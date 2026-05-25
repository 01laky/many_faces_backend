using Microsoft.Extensions.Logging;

namespace BeDemo.Api.Tests.Testing;

/// <summary>
/// In-memory <see cref="ILogger{T}"/> that records formatted log messages for assertions.
/// </summary>
/// <remarks>
/// Used by security tests (e.g. SHV2 PI-7) to prove sensitive strings never reach log output.
/// </remarks>
internal sealed class CollectingLogger<T> : ILogger<T>
{
	public List<(LogLevel Level, string Message)> Entries { get; } = [];

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		Entries.Add((logLevel, formatter(state, exception)));
	}
}
