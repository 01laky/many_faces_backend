using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP21 edge cases — baseline script contract and optional live host run.</summary>
public sealed class BeRp21BaselineScriptEdgeTests
{
	/// <summary>BE-RP21-U2 — output JSON schema fields are stable in script source.</summary>
	[Fact]
	public void BE_RP21_U2_BaselineScript_DefinesStableJsonSchema()
	{
		var script = File.ReadAllText(MonorepoPaths.BaselineScriptPath);
		foreach (var token in new[]
				 {
					 "schemaVersion", "engagement", "BE-RP21", "generatedAt", "baseUrl",
					 "samplesPerEndpoint", "endpoints", "p50Ms", "p95Ms", "errors",
				 })
		{
			script.Should().Contain(token);
		}
	}

	/// <summary>BE-RP21-U1 — script exits 0 against live API when BE_PERF_BASE_URL is set (TestServer is not reachable from Node).</summary>
	[Fact]
	public async Task BE_RP21_U1_ScriptExitsZero_AgainstTestHost()
	{
		if (!TryFindNodeExecutable(out var node))
			return;

		var baseUrl = Environment.GetEnvironmentVariable("BE_PERF_BASE_URL");
		if (string.IsNullOrWhiteSpace(baseUrl))
			return;

		baseUrl = baseUrl.Trim().TrimEnd('/');

		var psi = new ProcessStartInfo
		{
			FileName = node,
			Arguments = $"\"{MonorepoPaths.BaselineScriptPath}\" \"{baseUrl}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		psi.Environment["BE_PERF_SAMPLES"] = "10";
		psi.Environment["BE_PERF_EMAIL"] = IntegrationTestSeed.SuperAdminEmail;
		psi.Environment["BE_PERF_PASSWORD"] = IntegrationTestSeed.Password;
		psi.Environment["BE_PERF_CLIENT_ID"] = "be-demo-client";
		psi.Environment["BE_PERF_CLIENT_SECRET"] = "be-demo-secret-very-strong-key";
		psi.Environment["BE_PERF_FACE_PREFIX"] = "public";

		using var process = Process.Start(psi)!;
		var stdout = await process.StandardOutput.ReadToEndAsync();
		var stderr = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		process.ExitCode.Should().Be(0, $"stderr: {stderr}");

		var summary = JsonSerializer.Deserialize<JsonElement>(stdout);
		summary.GetProperty("schemaVersion").GetInt32().Should().Be(1);
		summary.GetProperty("engagement").GetString().Should().Be("BE-RP21");
		summary.GetProperty("endpoints").GetArrayLength().Should().BeGreaterThan(0);
	}

	private static bool TryFindNodeExecutable(out string nodePath)
	{
		nodePath = "node";
		try
		{
			using var which = Process.Start(new ProcessStartInfo
			{
				FileName = "which",
				Arguments = "node",
				RedirectStandardOutput = true,
				UseShellExecute = false,
			});
			if (which is null)
				return false;

			var path = which.StandardOutput.ReadToEnd().Trim();
			which.WaitForExit(2000);
			if (which.ExitCode != 0 || string.IsNullOrEmpty(path))
				return false;

			nodePath = path;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
