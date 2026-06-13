using BeDemo.Api.Models.DTOs.Moderation;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the in-memory moderation queue sort (previously untested): the default order is
/// submittedAtUtc descending (falling back to CreatedAt), and explicit fields sort ascending unless
/// sortDir is "desc".
/// </summary>
public sealed class ModerationQueueSorterTests
{
	private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

	private static ModerationItemDto Item(
		int contentId = 1,
		string title = "t",
		int faceId = 1,
		DateTime? submittedAtUtc = null,
		DateTime? createdAt = null)
		=> new(
			ContentType: default,
			ContentId: contentId,
			Title: title,
			FaceId: faceId,
			FaceTitle: "f",
			CreatorId: "c",
			CreatorName: "n",
			ApprovalStatus: default,
			AiReviewStatus: default,
			AiReviewDecision: default,
			AiReviewConfidence: null,
			AiReviewRiskLevel: default,
			AiReviewFlagsJson: null,
			AiReviewReason: null,
			AiReviewUserMessage: null,
			AiReviewModelVersion: null,
			AiReviewTraceId: null,
			SubmittedAtUtc: submittedAtUtc,
			HumanReviewedAtUtc: null,
			HumanDecisionReason: null,
			RemovedAtUtc: null,
			RemovalReason: null,
			CreatedAt: createdAt ?? Utc(2026, 1, 1),
			BodyPreviewPlainText: "",
			MediaUrlPreview: null);

	[Fact]
	public void Default_sort_is_submitted_at_descending()
	{
		var items = new[]
		{
			Item(contentId: 1, submittedAtUtc: Utc(2026, 1, 1)),
			Item(contentId: 2, submittedAtUtc: Utc(2026, 1, 3)),
			Item(contentId: 3, submittedAtUtc: Utc(2026, 1, 2)),
		};

		ModerationQueueSorter.ApplySort(items, null, null)
			.Select(i => i.ContentId).Should().Equal(2, 3, 1);
	}

	[Fact]
	public void Default_sort_falls_back_to_created_at_when_submitted_is_null()
	{
		var items = new[]
		{
			Item(contentId: 1, submittedAtUtc: null, createdAt: Utc(2026, 1, 5)),
			Item(contentId: 2, submittedAtUtc: Utc(2026, 1, 1)),
		};

		ModerationQueueSorter.ApplySort(items, null, null)
			.Select(i => i.ContentId).Should().Equal(1, 2);
	}

	[Fact]
	public void ContentId_sorts_ascending_by_default_and_descending_on_desc()
	{
		var items = new[] { Item(contentId: 3), Item(contentId: 1), Item(contentId: 2) };

		ModerationQueueSorter.ApplySort(items, "contentid", "asc")
			.Select(i => i.ContentId).Should().Equal(1, 2, 3);

		ModerationQueueSorter.ApplySort(items, "ContentId", "desc")
			.Select(i => i.ContentId).Should().Equal(3, 2, 1);
	}

	[Fact]
	public void Title_sorts_ascending()
	{
		var items = new[] { Item(contentId: 1, title: "b"), Item(contentId: 2, title: "a"), Item(contentId: 3, title: "c") };

		ModerationQueueSorter.ApplySort(items, "title", null)
			.Select(i => i.Title).Should().Equal("a", "b", "c");
	}

	[Fact]
	public void Unknown_sort_field_falls_back_to_the_default_order()
	{
		var items = new[]
		{
			Item(contentId: 1, submittedAtUtc: Utc(2026, 1, 1)),
			Item(contentId: 2, submittedAtUtc: Utc(2026, 1, 3)),
		};

		ModerationQueueSorter.ApplySort(items, "nonexistent", null)
			.Select(i => i.ContentId).Should().Equal(2, 1);
	}
}
