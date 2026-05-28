using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP30 edge cases (BE-RP30-U1) — read-replica runbook completeness.</summary>
public sealed class BeRp30ReadReplicaRunbookEdgeTests
{
	/// <summary>BE-RP30-U1 — runbook classifies audit flows as replica-safe or primary-only.</summary>
	[Fact]
	public void BE_RP30_U1_Runbook_ListsAuditFlowsWithRoutingGuidance()
	{
		var runbook = File.ReadAllText(MonorepoPaths.ReadReplicaRunbookPath);
		runbook.Should().Contain("BE-RP30");
		runbook.Should().Contain("Replica-safe");
		runbook.Should().Contain("Primary-only");

		foreach (var flow in new[]
				 {
					 "GET /api/localization",
					 "GET /{face}/api/faces/config",
					 "GET /api/Stats/public",
					 "grid snapshot",
					 "POST /api/oauth2/token",
					 "GET /api/me/capabilities",
					 "GET /api/messages/conversations",
					 "Search outbox processor",
				 })
		{
			runbook.Should().Contain(flow, $"runbook should mention {flow}");
		}
	}
}
