namespace BeDemo.Api.Tests.Performance;

internal static class MonorepoPaths
{
	private static readonly string MarkerRelative = Path.Combine("scripts", "backend-perf-baseline.mjs");

	public static string FindRoot()
	{
		if (TryFindRoot(out var root))
			return root;

		throw new InvalidOperationException(
			"Monorepo root not found (scripts/backend-perf-baseline.mjs). " +
			"Set MANY_FACES_MONOREPO_ROOT or ensure Fixtures/Monorepo is copied to test output.");
	}

	public static bool TryFindRoot(out string root)
	{
		var envRoot = Environment.GetEnvironmentVariable("MANY_FACES_MONOREPO_ROOT");
		if (!string.IsNullOrWhiteSpace(envRoot))
		{
			var candidate = Path.GetFullPath(envRoot);
			if (File.Exists(Path.Combine(candidate, MarkerRelative)))
			{
				root = candidate;
				return true;
			}
		}

		foreach (var start in EnumerateSearchDirectories())
		{
			var dir = start;
			while (dir is not null)
			{
				if (File.Exists(Path.Combine(dir.FullName, MarkerRelative)))
				{
					root = dir.FullName;
					return true;
				}

				if (File.Exists(Path.Combine(dir.FullName, "BeDemo.sln")))
				{
					var parent = dir.Parent;
					if (parent is not null && File.Exists(Path.Combine(parent.FullName, MarkerRelative)))
					{
						root = parent.FullName;
						return true;
					}
				}

				dir = dir.Parent;
			}
		}

		var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Monorepo");
		if (File.Exists(Path.Combine(fixtureRoot, MarkerRelative)))
		{
			root = fixtureRoot;
			return true;
		}

		root = string.Empty;
		return false;
	}

	private static IEnumerable<DirectoryInfo> EnumerateSearchDirectories()
	{
		yield return new DirectoryInfo(AppContext.BaseDirectory);

		var cwd = SafeGetCurrentDirectory();
		if (cwd is not null)
			yield return new DirectoryInfo(cwd);
	}

	private static string? SafeGetCurrentDirectory()
	{
		try
		{
			return Directory.GetCurrentDirectory();
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
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
