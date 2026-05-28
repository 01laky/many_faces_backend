using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP15 edge cases (BE-RP15-U1) — Npgsql pool tuning documented for ops.</summary>
public sealed class BeRp15ConnectionPoolEdgeTests
{
	/// <summary>BE-RP15-U1 — perf guide documents explicit Maximum/Minimum Pool Size on DefaultConnection.</summary>
	[Fact]
	public void BE_RP15_U1_PerfGuide_DocumentsExplicitPoolParams()
	{
		var guide = File.ReadAllText(MonorepoPaths.BackendPerformanceGuidePath);
		guide.Should().Contain("Maximum Pool Size");
		guide.Should().Contain("Minimum Pool Size");
		guide.Should().Contain("ConnectionStrings:DefaultConnection");
		guide.Should().Contain("BE-RP15");
	}
}
