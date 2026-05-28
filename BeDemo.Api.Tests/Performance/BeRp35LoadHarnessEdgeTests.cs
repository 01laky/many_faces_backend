using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP35 edge cases — k6 load harness contract (optional live run waived in CI).</summary>
public sealed class BeRp35LoadHarnessEdgeTests
{
	/// <summary>BE-RP35-U2 — harness defines stable summary JSON schema in script.</summary>
	[Fact]
	public void BE_RP35_U2_LoadHarness_DefinesStableSummarySchema()
	{
		var script = File.ReadAllText(MonorepoPaths.LoadHarnessScriptPath);
		foreach (var token in new[]
				 {
					 "schemaVersion", "engagement", "BE-RP35", "generatedAt", "baseUrl",
					 "handleSummary", "errors", "hot_path_latency_ms", "auth_latency_ms",
				 })
		{
			script.Should().Contain(token);
		}

		script.Should().Contain("rate<0.01", "auth failure SLO threshold documented");
	}

	/// <summary>BE-RP35-U3 — script uses demo OAuth fixture credentials (0% auth failure when stack seeded).</summary>
	[Fact]
	public void BE_RP35_U3_LoadHarness_UsesValidDemoCredentialsFixture()
	{
		var script = File.ReadAllText(MonorepoPaths.LoadHarnessScriptPath);
		script.Should().Contain("BE_PERF_EMAIL");
		script.Should().Contain("BE_PERF_PASSWORD");
		script.Should().Contain("BE_PERF_CLIENT_SECRET");
		script.Should().Contain("grantType");
		script.Should().Contain("accessToken");
	}

	/// <summary>BE-RP35-U1 — k6 script exists and references mixed traffic profile (live run requires k6 + API).</summary>
	[Fact]
	public void BE_RP35_U1_LoadHarness_ScriptPresentWithMixedTrafficProfile()
	{
		File.Exists(MonorepoPaths.LoadHarnessScriptPath).Should().BeTrue();
		var script = File.ReadAllText(MonorepoPaths.LoadHarnessScriptPath);
		script.Should().Contain("constant-vus");
		script.Should().Contain("/api/profile/me");
		script.Should().Contain("grid-snapshot");
	}
}
