using BeDemo.Api.Models.Requests.Moderation;
using BeDemo.Api.Validation.Moderation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Moderation;

public sealed class GetModerationQueueQueryValidatorTests
{
	private readonly GetModerationQueueQueryValidator _sut = new();

	[Fact]
	public void Defaults_are_valid()
	{
		_sut.TestValidate(new GetModerationQueueQuery()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void MinConfidence_greater_than_max_fails()
	{
		_sut.TestValidate(new GetModerationQueueQuery { MinConfidence = 0.9, MaxConfidence = 0.1 })
			.ShouldHaveValidationErrorFor(x => x);
	}

	[Fact]
	public void FaceId_zero_fails()
	{
		_sut.TestValidate(new GetModerationQueueQuery { FaceId = 0 }).ShouldHaveValidationErrorFor(x => x.FaceId);
	}

	[Fact]
	public void Invalid_sortBy_fails()
	{
		_sut.TestValidate(new GetModerationQueueQuery { SortBy = "creatorName", SortDir = "asc" })
			.ShouldHaveValidationErrorFor(x => x.SortBy);
	}

	[Theory]
	[InlineData("submittedAtUtc", "desc")]
	[InlineData("riskLevel", "asc")]
	public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
	{
		_sut.TestValidate(new GetModerationQueueQuery { SortBy = sortBy, SortDir = sortDir })
			.ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void FlagContains_over_max_length_fails()
	{
		_sut.TestValidate(new GetModerationQueueQuery { FlagContains = new string('f', 201) })
			.ShouldHaveValidationErrorFor(x => x.FlagContains);
	}

	[Fact]
	public void ContentId_zero_fails()
	{
		_sut.TestValidate(new GetModerationQueueQuery { ContentId = 0 })
			.ShouldHaveValidationErrorFor(x => x.ContentId);
	}

	[Fact]
	public void ContentId_positive_is_valid()
	{
		_sut.TestValidate(new GetModerationQueueQuery { ContentId = 42 })
			.ShouldNotHaveValidationErrorFor(x => x.ContentId);
	}
}
