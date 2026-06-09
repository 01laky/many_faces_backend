using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Services;

/// <summary>
/// Characterization tests for the destructive, dry-run-gated <see cref="ContentRetentionCleanupService"/>
/// (backend-refactor §4.2, 0 tests): only Rejected/Removed content past the retention cutoff is eligible; a dry run
/// counts but persists nothing; an execute clears the retained AI-review fields. Data-loss-capable code MUST be tested.
/// </summary>
public sealed class ContentRetentionCleanupServiceTests
{
	private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
	private const int Retention = 180;

	private static Blog Blog(int id, ContentApprovalStatus status, DateTime? humanReviewed, DateTime? removed = null) => new()
	{
		Id = id,
		FaceId = 1,
		CreatorId = "creator-1",
		ApprovalStatus = status,
		HumanReviewedAtUtc = humanReviewed,
		RemovedAtUtc = removed,
		AiReviewReason = "leaked reason",
		AiReviewTraceId = "trace-1",
		AiReviewModelVersion = "v1",
	};

	[Fact]
	public async Task Only_rejected_or_removed_past_cutoff_are_eligible()
	{
		var name = $"ret-{Guid.NewGuid():N}";
		await using (var seed = InMemoryDb.Named(name))
		{
			seed.Blogs.Add(Blog(1, ContentApprovalStatus.Rejected, Now.AddDays(-Retention - 5)));                     // eligible
			seed.Blogs.Add(Blog(2, ContentApprovalStatus.Removed, humanReviewed: null, removed: Now.AddDays(-Retention - 5))); // eligible
			seed.Blogs.Add(Blog(3, ContentApprovalStatus.Rejected, Now.AddDays(-10)));                                // too recent
			seed.Blogs.Add(Blog(4, ContentApprovalStatus.Approved, Now.AddDays(-Retention - 5)));                     // not rejected/removed
			await seed.SaveChangesAsync();
		}
		await using var run = InMemoryDb.Named(name);
		var result = await new ContentRetentionCleanupService(run).RunAsync(dryRun: false, Now);
		result.BlogsRedacted.Should().Be(2);
	}

	[Fact]
	public async Task Dry_run_counts_match_execute_but_persist_nothing()
	{
		var name = $"ret-{Guid.NewGuid():N}";
		await using (var seed = InMemoryDb.Named(name))
		{
			seed.Blogs.Add(Blog(1, ContentApprovalStatus.Rejected, Now.AddDays(-Retention - 1)));
			await seed.SaveChangesAsync();
		}

		await using (var dry = InMemoryDb.Named(name))
			(await new ContentRetentionCleanupService(dry).RunAsync(dryRun: true, Now)).BlogsRedacted.Should().Be(1);

		await using (var afterDry = InMemoryDb.Named(name))
			(await afterDry.Blogs.FindAsync(1))!.AiReviewReason.Should().Be("leaked reason", "a dry run must not redact anything");

		await using (var exec = InMemoryDb.Named(name))
			(await new ContentRetentionCleanupService(exec).RunAsync(dryRun: false, Now)).BlogsRedacted.Should().Be(1);

		await using (var afterExec = InMemoryDb.Named(name))
		{
			var blog = await afterExec.Blogs.FindAsync(1);
			blog!.AiReviewReason.Should().BeNull("execute clears the retained AI-review fields");
			blog.AiReviewTraceId.Should().BeNull();
			blog.AiReviewModelVersion.Should().BeNull();
		}
	}

	[Fact]
	public async Task Nothing_eligible_returns_zero()
	{
		await using var db = InMemoryDb.Fresh();
		var result = await new ContentRetentionCleanupService(db).RunAsync(dryRun: false, Now);
		result.Should().Be(new RetentionCleanupResult(0, 0, 0));
	}
}
