using BeDemo.Api.Services;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public sealed class ContentModerationAlertEvaluatorTests
{
	private static ContentModerationMetricsSnapshot EmptySnapshot() =>
		new(
			PendingSubmissions: 0,
			AiQueuedJobs: 0,
			AiProcessingJobs: 0,
			AiFailedJobs: 0,
			OldestPendingSubmissionUtc: null,
			OldestPendingAgeHours: null,
			AverageReviewLatencyHours: null,
			P95ReviewLatencyHours: null,
			ApprovedCount: 0,
			RejectedCount: 0,
			RemovedCount: 0,
			RecommendedApproveCount: 0,
			RecommendedRejectCount: 0,
			NeedsHumanReviewCount: 0,
			AiJobsLikelyTimeoutCount: 0,
			TopModerationFlags: Array.Empty<FlagCountDto>(),
			PendingSubmissionsByFace: Array.Empty<FacePendingCountDto>());

	[Fact]
	public void Evaluate_ShouldReturnEmpty_WhenMetricsBelowThresholds()
	{
		var alerts = ContentModerationAlertEvaluator.Evaluate(EmptySnapshot(), DateTime.UtcNow);
		alerts.Should().BeEmpty();
	}

	[Fact]
	public void Evaluate_ShouldEmitOldestPending_WhenAgeAtLeast24Hours()
	{
		var snapshot = EmptySnapshot() with { OldestPendingAgeHours = 24 };
		var alerts = ContentModerationAlertEvaluator.Evaluate(snapshot, DateTime.UtcNow);
		alerts.Should().ContainSingle(a => a.Code == ContentModerationAlertEvaluator.OldestPendingExceeded);
	}

	[Theory]
	[InlineData(2, false)]
	[InlineData(3, true)]
	public void Evaluate_ShouldEmitAiFailedJobs_WhenAtOrAboveThree(int failed, bool expected)
	{
		var snapshot = EmptySnapshot() with { AiFailedJobs = failed };
		var alerts = ContentModerationAlertEvaluator.Evaluate(snapshot, DateTime.UtcNow);
		alerts.Any(a => a.Code == ContentModerationAlertEvaluator.AiFailedJobs).Should().Be(expected);
	}

	[Theory]
	[InlineData(49, false)]
	[InlineData(50, true)]
	public void Evaluate_ShouldEmitHighPendingVolume_WhenAtOrAboveFifty(int pending, bool expected)
	{
		var snapshot = EmptySnapshot() with { PendingSubmissions = pending };
		var alerts = ContentModerationAlertEvaluator.Evaluate(snapshot, DateTime.UtcNow);
		alerts.Any(a => a.Code == ContentModerationAlertEvaluator.HighPendingVolume).Should().Be(expected);
	}

	[Fact]
	public void Evaluate_ShouldReturnMultipleAlerts_WhenSeveralThresholdsMet()
	{
		var snapshot = EmptySnapshot() with
		{
			OldestPendingAgeHours = 30,
			AiFailedJobs = 5,
			PendingSubmissions = 60,
		};
		var alerts = ContentModerationAlertEvaluator.Evaluate(snapshot, DateTime.UtcNow);
		alerts.Select(a => a.Code).Should().BeEquivalentTo(new[]
		{
			ContentModerationAlertEvaluator.OldestPendingExceeded,
			ContentModerationAlertEvaluator.AiFailedJobs,
			ContentModerationAlertEvaluator.HighPendingVolume,
		});
	}
}
