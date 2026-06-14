using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for story face targeting (previously untested): no StoryFace rows means the story
/// targets every face; otherwise only the listed faces, and a null viewer face never matches a targeted story.
/// </summary>
public sealed class StoryVisibilityTests
{
	private static Story StoryWithFaces(params int[] faceIds) =>
		new() { StoryFaces = faceIds.Select(id => new StoryFace { FaceId = id }).ToList() };

	[Fact]
	public void Untargeted_story_is_visible_to_every_face_including_null()
	{
		var story = new Story(); // no StoryFaces
		StoryVisibility.IsTargetedForFace(story, 1).Should().BeTrue();
		StoryVisibility.IsTargetedForFace(story, null).Should().BeTrue();
	}

	[Fact]
	public void Targeted_story_matches_only_listed_faces()
	{
		var story = StoryWithFaces(2, 5);
		StoryVisibility.IsTargetedForFace(story, 2).Should().BeTrue();
		StoryVisibility.IsTargetedForFace(story, 5).Should().BeTrue();
		StoryVisibility.IsTargetedForFace(story, 3).Should().BeFalse();
	}

	[Fact]
	public void Targeted_story_never_matches_a_null_face()
	{
		StoryVisibility.IsTargetedForFace(StoryWithFaces(2), null).Should().BeFalse();
	}
}
