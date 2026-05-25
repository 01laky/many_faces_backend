using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class StoryVisibility
{
	/// <summary>No <see cref="StoryFace"/> rows → story targets every face; otherwise only listed faces.</summary>
	public static bool IsTargetedForFace(Story story, int? faceId)
	{
		if (story.StoryFaces == null || story.StoryFaces.Count == 0)
			return true;

		return faceId.HasValue && story.StoryFaces.Any(sf => sf.FaceId == faceId.Value);
	}
}
