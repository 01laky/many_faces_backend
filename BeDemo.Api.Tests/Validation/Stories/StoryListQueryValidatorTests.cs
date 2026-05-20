using BeDemo.Api.Validation.Stories;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Stories;

public sealed class StoryListQueryValidatorTests
{
    private readonly StoryListQueryValidator _sut = new();

    [Fact]
    public void Valid_faceId_has_no_errors()
    {
        var result = _sut.TestValidate(new BeDemo.Api.Models.Requests.Stories.StoryListQuery { FaceId = 1 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_creatorId_only_has_no_errors()
    {
        var result = _sut.TestValidate(new BeDemo.Api.Models.Requests.Stories.StoryListQuery { CreatorId = "user-1" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Missing_face_and_creator_fails()
    {
        var result = _sut.TestValidate(new BeDemo.Api.Models.Requests.Stories.StoryListQuery());
        result.ShouldHaveValidationErrorFor(x => x);
    }
}
