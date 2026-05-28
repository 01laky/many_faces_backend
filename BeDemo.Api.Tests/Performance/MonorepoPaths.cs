namespace BeDemo.Api.Tests.Performance;

internal static class MonorepoPaths
{
	public static string FindRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "scripts", "backend-perf-baseline.mjs")))
				return dir.FullName;

			dir = dir.Parent;
		}

		throw new InvalidOperationException("Monorepo root not found (scripts/backend-perf-baseline.mjs).");
	}

	public static string BaselineScriptPath => Path.Combine(FindRoot(), "scripts", "backend-perf-baseline.mjs");

	public static string LoadHarnessScriptPath => Path.Combine(FindRoot(), "scripts", "backend-load-test.k6.js");

	public static string BackendPerformanceGuidePath =>
		Path.Combine(FindRoot(), "docs", "guides", "backend-performance.md");

	public static string ReadReplicaRunbookPath =>
		Path.Combine(FindRoot(), "docs", "guides", "backend-read-replica.md");
}

internal sealed class FixedOptionsMonitor<T> : Microsoft.Extensions.Options.IOptionsMonitor<T>
	where T : class
{
	public FixedOptionsMonitor(T value) => CurrentValue = value;

	public T CurrentValue { get; }

	public T Get(string? name) => CurrentValue;

	public IDisposable? OnChange(Action<T, string?> listener) => null;
}
